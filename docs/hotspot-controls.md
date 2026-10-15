# Hotspot controls and state consistency

This repairs reproducible interaction defects found while reviewing the remaining reports. It does not claim to diagnose the blue-character report in upstream #85; that report has no reproducible cause established here.

## Defects and causes

| Trigger | Previous behavior | Cause |
| --- | --- | --- |
| Use **Disable Hotspots** or press **R** with hotspots enabled | The two lower emote regions could remain active. | Only the left, right and top regions had `IsEnabled` toggled; both lower regions were omitted. |
| Start with `DISABLE_HOTSPOTS=true` or `ALLOW_COLOR_HOTSPOT=true`, then toggle | The first toggle could disagree with the configured appearance; colors changed unexpectedly. | The controller always started its own flags at false, independently of config-applied WPF state. |
| Toggle through the keyboard, then inspect the tray | Checkmarks did not reflect the actual state. | The tray inverted its own checkboxes only when its menu item was clicked. |
| Show colors, disable and re-enable hotspots | The color preference and displayed palette drifted. | Separate routines assigned different palettes and overwrote backgrounds without reconciling enabled/color state. |
| Disable a region | Its overlay could still participate in hit testing. | The old implementation did not disable `IsHitTestVisible`; a transparent WPF background is still an input surface. |

## Corrected behavior

The hotspot controller reads both config flags at construction and applies one state to all five regions. `ConfigManager` still applies layout/scaling, but no longer maintains a second copy of hotspot appearance/enabled state.

- **Disable Hotspots** and **R** toggle all five regions together. Disabled regions have both `IsEnabled=false` and `IsHitTestVisible=false`, allowing input to reach the underlying sprite.
- **Show Hotspots** controls color preference. When hotspots are enabled, uncolored regions retain the existing almost-transparent `#01000000` hit surface. Colored regions use one stable palette: left red, right blue, top purple, lower-left yellow, lower-right orange.
- Disabled regions stay clear even if a color preference is stored. The tray's color command is unavailable while hotspots are disabled; its checkmark retains the preference that will be restored on re-enabling.
- Tray checkmarks initialize from the active controller and refresh after both menu and keyboard-entry-point toggles. They no longer keep an independent toggle state.
- Runtime config flags reflect the active state, but these controls never rewrite `config.txt`. Restart still reloads the saved flags.

Example saved settings:

```ini
DISABLE_HOTSPOTS = false
ALLOW_COLOR_HOTSPOT = false
```

Disabling hotspots controls those mouse regions. Existing keyboard emote shortcuts and other sprite actions remain independent. Keyboard controls require focus and `ENABLE_KEYBOARD=true`; click-through mode can prevent normal mouse interaction/focus, as before.

## Regression checks

The initial Windows regression test is committed before the production fix. It exercises the original controller and requires all five regions to be disabled, demonstrating the omitted lower regions against the actual WPF code.

Expanded Windows tests cover every combination of the two startup flags, disable/show/re-enable sequences, stable colors, synchronized runtime flags and WPF `InputHitTest` results at every region. Hit testing must choose the active hotspot when enabled and the underlying surface when disabled.

Integration tests construct real main windows from all four config combinations, inspect the actual native tray menu, invoke its menu actions and the main-window entry point used by **R**, verify every named XAML region and assert that saved config contents are unchanged. Existing startup, sprite decoding and companion-isolation tests remain enabled.

Run on Windows:

```powershell
msbuild Desktop_Gremlin.sln /m /p:Configuration=Release
dotnet run --project tests/DesktopGremlin.WindowsTests -c Release
```

CI also retains the portable .NET 8 Windows/Linux and .NET Framework 4.8 regression checks. These fixes affect rebuilt source executables; already downloaded release binaries are unchanged.
