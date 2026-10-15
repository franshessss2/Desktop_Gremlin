#requires -Version 5.1
[CmdletBinding()]
param([string]$ArchivePath, [string]$FixturePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$repair = Join-Path $repository 'tools/Repair-GoldShipPack.ps1'
$profile = Get-Content (Join-Path $repository 'pack-fixes/GoldShip-v4.0/profile.json') -Raw | ConvertFrom-Json
$work = Join-Path ([IO.Path]::GetTempPath()) ('gremlin-goldship-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
$script:assertions = 0
function Assert([bool]$condition, [string]$message) {
    $script:assertions++
    if (!$condition) { throw $message }
}
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
try {
    if (!$ArchivePath) {
        $ArchivePath = Join-Path $work 'TracenAcademy_v4.0.zip'
        Invoke-WebRequest 'https://github.com/Kritzkingvoid/Desktop_Gremlin/releases/download/v4.0/TracenAcademy_v4.0.zip' -OutFile $ArchivePath -UseBasicParsing
    }
    Assert ((Hash $ArchivePath) -eq $profile.source_archive_sha256) 'Pinned release archive changed.'
    $pack = Join-Path $work 'installation'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $ArchivePath).Path)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '^TracenAcademy_v4\.0/((SpriteSheet/Gremlins|Sounds)/GoldShip/.+\.(png|txt|wav))$') {
                $destination = Join-Path $pack $matches[1]
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
            }
        }
    }
    finally { $zip.Dispose() }
    $config = Join-Path $pack $profile.config
    $sound = Join-Path $pack $profile.sound.path
    # Customized scale, newline style and UTF-8 BOM must survive.
    $customized = [IO.File]::ReadAllText($config).Replace('SCALE = 0.9', 'SCALE = 0,7') + "`r`n# custom comment`r`nCUSTOM_KEY=keep`r`n"
    [IO.File]::WriteAllText($config, $customized, (New-Object Text.UTF8Encoding($true)))
    $before = Hash $config
    $plan = & $repair -InstallationPath $pack
    Assert (!$plan.Applied -and $plan.ConfigChanged -and $plan.SoundAdded) 'Dry run must report required fixes.'
    Assert ((Hash $config) -eq $before -and !(Test-Path $sound)) 'Dry run must not write.'
    Assert (@(Get-ChildItem $pack -Recurse -Filter '*.bak').Count -eq 0) 'Dry run must not create backups.'
    $result = & $repair -InstallationPath $pack -Apply
    Assert ($result.Applied -and (Hash $result.Backup) -eq $before) 'Apply must preserve an exact original backup.'
    $fixed = [IO.File]::ReadAllText($config)
    foreach ($property in $profile.settings.PSObject.Properties) {
        Assert ([regex]::Matches($fixed, '(?m)^' + $property.Name + '=' + $property.Value.after + '\r?$').Count -eq 1) ('Incorrect/duplicate setting: ' + $property.Name)
    }
    Assert ($fixed.Contains('SCALE = 0,7') -and $fixed.Contains("# custom comment`r`nCUSTOM_KEY=keep`r`n")) 'Custom settings must survive.'
    Assert ([IO.File]::ReadAllBytes($config)[0] -eq 239) 'UTF-8 BOM must survive.'
    Assert ((Hash $sound) -eq $profile.sound.sha256) 'Correct matching emote audio must be restored.'
    $fixedHash = Hash $config
    $fixedBytes = [IO.File]::ReadAllBytes($config)
    $again = & $repair -InstallationPath $pack -Apply
    Assert (!$again.ConfigChanged -and !$again.SoundAdded -and (Hash $config) -eq $fixedHash) 'Second apply must be idempotent.'
    Assert (@(Get-ChildItem $pack -Recurse -Filter '*.bak').Count -eq 1) 'Second apply must not create another backup.'
    [IO.File]::WriteAllText($sound, 'custom sound placeholder')
    $customSound = Hash $sound
    $null = & $repair -InstallationPath $pack -Apply
    Assert ((Hash $sound) -eq $customSound) 'Existing custom audio must be preserved.'
    [IO.File]::Delete($sound)
    [IO.File]::WriteAllText($config, $fixed.Replace('HOVER=25', 'HOVER=24'))
    $customConfig = Hash $config
    $rejected = $false
    try { $null = & $repair -InstallationPath $pack -Apply } catch { $rejected = $_.Exception.Message.Contains('Unexpected HOVER') }
    Assert ($rejected -and (Hash $config) -eq $customConfig -and !(Test-Path $sound)) 'Unexpected counts must be rejected before writes.'
    [IO.File]::WriteAllBytes($config, $fixedBytes)
    $sprite = Join-Path $pack $profile.sprites[0].path
    $originalSprite = [IO.File]::ReadAllBytes($sprite)
    [IO.File]::WriteAllText($sprite, 'unknown artwork')
    $rejected = $false
    try { $null = & $repair -InstallationPath $pack -Apply } catch { $rejected = $_.Exception.Message.Contains('Unrecognized') }
    Assert ($rejected -and (Hash $config) -eq $fixedHash -and !(Test-Path $sound)) 'Unknown artwork must be rejected before writes.'
    [IO.File]::WriteAllBytes($sprite, $originalSprite)
    if ($env:OS -eq 'Windows_NT') {
        [IO.File]::WriteAllText($config, $fixed.Replace('HOVER=25', 'HOVER=30'))
        $lockedHash = Hash $config
        $lock = [IO.File]::Open($config, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $failed = $false
        try {
            try { $null = & $repair -InstallationPath $pack -Apply } catch { $failed = $true }
        }
        finally { $lock.Dispose() }
        Assert ($failed -and (Hash $config) -eq $lockedHash -and !(Test-Path $sound)) 'Failed config replacement must roll back newly added audio.'
        Assert (@(Get-ChildItem $pack -Recurse -Filter '*.tmp').Count -eq 0) 'Failed writes must remove temporary files.'
        [IO.File]::WriteAllBytes($config, $fixedBytes)
    }
    $null = & $repair -InstallationPath $pack -Apply
    if ($FixturePath) {
        if (Test-Path $FixturePath) { throw 'FixturePath must not exist.' }
        Copy-Item $pack $FixturePath -Recurse
    }
    Write-Output "PASS: $script:assertions Gold Ship release repair assertions"
}
finally { Remove-Item $work -Recurse -Force }
