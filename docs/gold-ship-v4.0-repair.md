# Repair Gold Ship in Tracen Academy v4.0

This fixes the release data behind [upstream issue #68](https://github.com/Kritzkingvoid/Desktop_Gremlin/issues/68). The source repository does not include Gold Ship's complete artwork, so rebuilding the app alone cannot repair an already extracted pack. This repair targets the exact Gold Ship sheets shipped in `TracenAcademy_v4.0.zip`; it does not change executables or other characters.

## Run the repair

Close Desktop Gremlin. Download/extract this repository, keeping `tools` and `pack-fixes` together. Open PowerShell in that repository and pass the installation folder containing `SpriteSheet` and `Sounds`:

```powershell
# Preview only; creates no files.
.\tools\Repair-GoldShipPack.ps1 -InstallationPath 'C:\Games\TracenAcademy_v4.0'

# Apply after reviewing the preview.
.\tools\Repair-GoldShipPack.ps1 -InstallationPath 'C:\Games\TracenAcademy_v4.0' -Apply
```

Requires Windows PowerShell 5.1 or PowerShell 7. The script makes no network requests. If Windows marks the downloaded script as blocked, review it and use `Unblock-File .\tools\Repair-GoldShipPack.ps1` before running it. Restart the app afterward so it reloads the config and audio.

A changed config gets an exact backup beside it named `config.txt.before-pr3-<unique-id>.bak`. To undo, close the app, restore that backup to `config.txt`, and remove `Sounds\GoldShip\emote4.wav` only if this repair added it. Existing audio is preserved, including customized audio. Reapplying a completed repair makes no further changes.

## Causes and corrections

| Setting/file | Released value | Corrected value | Evidence |
| --- | --- | --- | --- |
| HOVER | 30 | 25 | Last five cells are fully transparent padding; standalone release declares 25 on identical artwork. |
| SLEEP | 255 | 50 | Sheet holds only 50 frames; standalone release declares 50 on identical artwork. |
| OUTRO | 65, then 68 | One entry: 35 | Both values exceed the 60-cell sheet; original pack declares 35 on identical artwork. Capacity alone does not establish the intended count. |
| GRAB | 40 | 60 | Original pack declares 60 on identical artwork; compilation truncated the animation. |
| RUNIDLE | 41 | 138 | Original `WALK_IDLE=138` uses identical `wIdle.png` artwork; last two of 140 cells are padding. |
| emote4.wav | Missing | Original matching audio | Compiled `Emotes/emote4.png` is identical to standalone `emote1.png`, so the corresponding original **emote1.wav** is restored under the new name. |

Hover loops into blank frames, producing flicker. Sleep/outro counts read beyond their sheets: older players can freeze; PR #2's validated renderer falls back to idle instead. Neither renderer behavior can recover the intended pack metadata or missing audio. The patch restores those data explicitly rather than guessing frame counts from image dimensions.

## Provenance and safeguards

`pack-fixes/GoldShip-v4.0/profile.json` records six sprite SHA-256 hashes, expected geometry, old/new counts and recovered audio provenance. The bundled WAV is unchanged original release data, attributed to the original Desktop Gremlin project and its asset creators. No artwork or audio was generated.

- Affected archive: [TracenAcademy v4.0](https://github.com/Kritzkingvoid/Desktop_Gremlin/releases/tag/v4.0), SHA-256 `0fa410c9bc983e39efb6cb905c7cefa65bf5000a4aa677b14ee7b5baa711be0d`.
- Reference archive: [GoldShip v2.5](https://github.com/Kritzkingvoid/Desktop_Gremlin/releases/tag/v2.5), SHA-256 `8dbe6493663cf7fc20e55885c6d26d02bd762c53a336c656c5f53c44b415cca3`.
- Audio entry: `GoldShip_v2.5/Sounds/GoldShip/emote1.wav`, SHA-256 `865dc3a9df56c7c095709b2645cc1e35e59840bfb3076912d7287c68e69a985f`.

Before writing, the script checks every affected sheet, geometry, expected counts and bundled WAV. Unknown artwork or custom affected counts cause a clear rejection with no repair writes. Unrelated config lines, scale, newline style and UTF-8 BOM survive. Duplicate affected keys are consolidated. Writes use temporary files, exclusive file creation and atomic config replacement; if config replacement fails, newly added sound is removed. Backups are never overwritten. Keep the app closed during repair.

## Regression prevention

Windows CI downloads the pinned v4.0 archive, verifies its SHA-256 and extracts only Gold Ship data files; no archived executable is run. `tests/Test-GoldShipPackRepair.ps1` checks preview, exact backup, corrections, duplicate removal, BOM/custom setting preservation, correct audio, idempotence, custom audio preservation and rejection of unknown artwork/counts. It runs under Windows PowerShell 5.1.

The WPF runner then loads that repaired pack through the production config parser and sprite renderer, plays two complete cycles of every repaired animation, checks that all hover frames contain visible pixels, plays all fourth-emote frames and validates the restored WAV. Existing portable, .NET Framework, startup and companion regression checks remain enabled.

For an offline/local run with an already downloaded archive:

```powershell
.\tests\Test-GoldShipPackRepair.ps1 -ArchivePath 'C:\Downloads\TracenAcademy_v4.0.zip' -FixturePath "$env:TEMP\goldship-repaired"
$env:GREMLIN_GOLDSHIP_RELEASE_DIR = "$env:TEMP\goldship-repaired"
# Build the WPF app with full MSBuild first, as in .github/workflows/regression.yml.
dotnet run --project tests/DesktopGremlin.WindowsTests -c Release
```

The fixture output folder must not already exist. Gold Ship playback checks explicitly report a skip if the environment variable is absent. CI always prepares the fixture first. This patch does not repair different/newer packs, invent missing pat artwork, change the general audio engine, or update previously distributed ZIPs automatically. Future release preparation must apply/include the corrected data and run these checks before packaging.
