# Safe timer and random configuration

This fixes concrete configuration crash paths found during source review. It does not claim to resolve a separate report whose cause has not been reproduced.

## Reproduction and causes

The original production loader accepted this file:

```ini
MIN_INTERVAL = 90
MAX_INTERVAL = 30
```

The same `Random.Next(Settings.RandomMinInterval, Settings.RandomMaxInterval)` call used by `AnimationController` then reproduced an `ArgumentOutOfRangeException`: `minValue cannot be greater than maxValue`. This can happen later on the dispatcher when random actions are enabled, rather than when config is read.

Other unsafe inputs reached consumers unchecked:

- Negative `SLEEP_TIME` throws during WPF timer setup. Zero creates a continuously due idle timer. Very large positive sleep values exceed the timer's signed 32-bit millisecond limit.
- Very high `SPRITE_FRAMERATE` values round `TimeSpan.FromMilliseconds(1000.0 / rate)` down to zero on .NET Framework, creating a continuously due animation/food/keyboard timer.
- Negative `SPAWN_DISTANCE` reverses random bounds. At `2147483647`, the old inclusive bound `radius + 1` overflows before `Random.Next` is called.
- Missing sleep/action interval keys previously defaulted to zero, producing immediate scheduling rather than a useful idle delay.

The timer interval limit is established in the [official WPF DispatcherTimer implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/DispatcherTimer.cs) and verified against the app's .NET Framework 4.8 runtime by the Windows tests.

## Rules after this change

| Setting | Accepted values | Missing-setting default |
| --- | --- | --- |
| `MIN_INTERVAL` | Positive 32-bit integer seconds | 30 |
| `MAX_INTERVAL` | Positive 32-bit integer seconds | 40 |
| `SLEEP_TIME` | 1–2,147,483 seconds | 500 |
| `SPRITE_FRAMERATE` | 1–1,000 frames/second | 60 |
| `SPAWN_DISTANCE` | 0–2,147,483,647 | 0 |
| `RANDOM_MOVE_DISTANCE` | 0–2,147,483,647 | 0 |

Invalid values retain the previous valid in-memory value; they do not overwrite the saved file. Defaults apply in a fresh process when the setting is omitted. Existing valid custom settings are retained within these ranges. Zero movement/spawn distance remains supported; zero time intervals are rejected.

After the complete file is loaded, `MIN_INTERVAL` is authoritative: if `MAX_INTERVAL` is lower, it is raised to the minimum. The example above therefore becomes a fixed 90-second delay in memory. This is stable across repeated loads and independent of line order. Normal ranges retain their existing exclusive-maximum behavior; `30..40` samples 30–39 seconds, while equal bounds are a fixed delay.

Ordinary spawn radii retain the existing integer RNG behavior. The largest radius uses a bounded integer-valued calculation without `radius + 1` overflow. The existing placement code still clamps the resulting position to the working area.

The 1,000 FPS limit prevents zero-millisecond timer requests; it does not promise a display refresh rate or precise timer frequency. These settings neither disable sleep nor disable random actions; `ALLOW_RANDOM_ACTIONS` remains the action toggle.

## Regression prevention

Portable tests link the actual loader and spawn helper. They cover the reproduced reversed range, both line orders, repeated loads, safe defaults, invalid values, valid/invalid WPF timer boundaries, the largest fixed action interval, inclusive spawn endpoints, zero radius, unchanged files and normal interval semantics.

Windows tests construct a real main window with reversed action bounds, negative sleep, an oversized frame rate and the largest spawn radius. They verify actual WPF timer intervals and work-area containment, exercise both scheduling branches of the production animation controller, and confirm that the config file stays unchanged. They also construct actual .NET Framework timers at the largest supported sleep duration and highest frame rate.

CI retains .NET 8 Windows/Linux regressions, Windows .NET Framework 4.8 regressions, the full WPF Release build, and existing startup/sprite/companion checks. Changes affect rebuilt executables; published ZIPs are unchanged.
