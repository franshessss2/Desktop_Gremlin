#requires -Version 5.1
<#
.SYNOPSIS
Repairs the verified Gold Ship pack from TracenAcademy v4.0.
.DESCRIPTION
Defaults to inspection. Close Desktop Gremlin, then use -Apply to save changes.
Unknown artwork/counts are rejected; custom audio and unrelated settings are preserved.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallationPath,
    [switch]$Apply
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$profileDirectory = Join-Path $PSScriptRoot '../pack-fixes/GoldShip-v4.0'
$profile = Get-Content -LiteralPath (Join-Path $profileDirectory 'profile.json') -Raw | ConvertFrom-Json
$root = (Resolve-Path -LiteralPath $InstallationPath).Path
function Get-PackPath([string]$relative) { Join-Path $root $relative }
function Assert-Hash([string]$path, [string]$expected) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) {
        throw "Unrecognized or missing asset: $path. No repair was applied."
    }
}
# Validate every prerequisite before creating directories, backups or temporary files.
foreach ($sprite in $profile.sprites) { Assert-Hash (Get-PackPath $sprite.path) $sprite.sha256 }
$sourceSound = Join-Path $profileDirectory $profile.sound.file
Assert-Hash $sourceSound $profile.sound.sha256
$configPath = Get-PackPath $profile.config
$originalBytes = [IO.File]::ReadAllBytes($configPath)
if ($originalBytes.Length -ge 2 -and
    (($originalBytes[0] -eq 255 -and $originalBytes[1] -eq 254) -or
     ($originalBytes[0] -eq 254 -and $originalBytes[1] -eq 255))) {
    throw 'Expected the UTF-8/ASCII release config. No repair was applied.'
}
$hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 239 -and
    $originalBytes[1] -eq 187 -and $originalBytes[2] -eq 191
$encoding = New-Object Text.UTF8Encoding($hasBom, $true)
$offset = 0
if ($hasBom) { $offset = 3 }
$original = $encoding.GetString($originalBytes, $offset, $originalBytes.Length - $offset)
$effective = @{}
foreach ($line in [regex]::Split($original, '\r\n|\n|\r')) {
    if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*([^=]+?)\s*$') {
        $effective[$matches[1].ToUpperInvariant()] = $matches[2]
    }
}
foreach ($property in $profile.geometry.PSObject.Properties) {
    if ($effective[$property.Name] -ne [string]$property.Value) {
        throw "Unexpected $($property.Name). No repair was applied."
    }
}
foreach ($property in $profile.settings.PSObject.Properties) {
    $value = $effective[$property.Name]
    if ($value -ne [string]$property.Value.before -and $value -ne [string]$property.Value.after) {
        throw "Unexpected $($property.Name)=$value. No repair was applied."
    }
}
# Match complete lines, including their original terminators. Keep all unrelated bytes/text.
$seen = @{}
$updated = [regex]::Replace($original, '(?m)^([^\r\n]*)(\r\n|\n|\r|$)', {
    param($match)
    $line = $match.Groups[1].Value
    if ($line -match '^\s*([A-Za-z0-9_]+)\s*=') {
        $key = $matches[1].ToUpperInvariant()
        $property = $profile.settings.PSObject.Properties[$key]
        if ($null -ne $property) {
            if ($seen.ContainsKey($key)) { return '' }
            $seen[$key] = $true
            return "$key=$($property.Value.after)" + $match.Groups[2].Value
        }
    }
    return $match.Value
})
$configChanged = $updated -cne $original
$soundPath = Get-PackPath $profile.sound.path
if ((Test-Path -LiteralPath $soundPath) -and !(Test-Path -LiteralPath $soundPath -PathType Leaf)) {
    throw "The audio destination is not a file: $soundPath. No repair was applied."
}
$addSound = !(Test-Path -LiteralPath $soundPath)
$report = [ordered]@{
    Installation = $root
    Applied = [bool]$Apply
    ConfigChanged = $configChanged
    SoundAdded = $addSound
    ExistingSoundPreserved = !$addSound
    Backup = $null
}
if (!$Apply -or (!$configChanged -and !$addSound)) { return [pscustomobject]$report }
$configTemp = $null
$soundTemp = $null
$soundAdded = $false
try {
    if ($configChanged) {
        $backup = $configPath + '.before-pr3-' + [guid]::NewGuid().ToString('N') + '.bak'
        [IO.File]::Copy($configPath, $backup, $false)
        $report.Backup = $backup
        $configTemp = $configPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
        [IO.File]::WriteAllBytes($configTemp, [byte[]]($encoding.GetPreamble() + $encoding.GetBytes($updated)))
    }
    if ($addSound) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($soundPath)) | Out-Null
        $soundTemp = $soundPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
        [IO.File]::Copy($sourceSound, $soundTemp, $false)
        [IO.File]::Move($soundTemp, $soundPath)
        $soundAdded = $true
    }
    if ($configChanged) { [IO.File]::Replace($configTemp, $configPath, [NullString]::Value) }
}
catch {
    if ($soundAdded) { [IO.File]::Delete($soundPath) }
    throw
}
finally {
    foreach ($temporary in @($configTemp, $soundTemp)) {
        if ($temporary -and [IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}
[pscustomobject]$report
