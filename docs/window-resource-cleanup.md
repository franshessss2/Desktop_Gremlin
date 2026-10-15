# Main-window resource cleanup

Closing the main gremlin window did not stop its animation/idle/gravity/movement timers or release the native tray component. With a companion or food window still open, WPF's default last-window shutdown policy could keep the process alive with a closed main gremlin still referenced by callbacks.

## Reproduced defect

A Windows regression was committed before the production fix. It constructs the real main window, verifies that its animation and idle timers are running, closes it and requires both timers to be stopped. The original source fails that assertion. The baseline is commit `9070e85e298be40610321a3fa2948e1a5d31202f`; its [CI run](https://github.com/franshessss2/Desktop_Gremlin/actions/runs/38747460798) records the failure.

The main window had no close cleanup. Its controllers created dispatcher timers, keyboard event subscriptions and a native tray icon/menu with no common teardown. Companion windows stopped their own timers when closed, but the main window did not close companions or food targets it had created.

## Changes

`MainWindow.OnClosed` now runs one idempotent cleanup routine before the base close notification:

- Stop main animation, idle and gravity timers and cancel the active random walk.
- Dispose the keyboard controller, stopping keyboard movement and detaching key/timer handlers so late input cannot restart work.
- Stop food following without playing an eating sound or starting another animation.
- Close the companion and food windows created by this main window and clear helper references. Companion close continues to use its existing timer cleanup.
- Dispose the native tray component and its menu. Repeated disposal is safe.

`TimerController.Stop`, `FoodFollower.Stop` and disposable keyboard/tray controllers provide the teardown operations. Normal food consumption still uses its existing sound/state transition; lifecycle cancellation is silent.

The application shutdown policy is preserved. Cleanup closes resources belonging to this window rather than forcing `Application.Shutdown`, so an explicit-shutdown host or another open main window is not terminated. Normal last-window shutdown can complete once the main gremlin and its helper windows are gone. No config file is changed.

## Regression prevention

Windows tests retain the original two-timer regression, then exercise a real visible main window with an active companion, food target, random walk, keyboard movement, gravity, animation and idle timing. They capture all eight active timer objects before close and require every timer to stop afterward.

The tests also verify helper-window removal and cleared references, disposal of the real native tray/menu exactly once, repeated cleanup, detached keyboard handlers, unchanged settings, and no continued movement/timers after another dispatcher pass. Both helper ordering cases are covered: helpers still open when the main closes, and helpers closed first.

Tests use small generated PNG fixtures in the test output and inspect real controller/timer/native menu objects. They do not require moving the user's mouse or listening to audio. The existing .NET 8 Windows/Linux, Windows .NET Framework 4.8, WPF build, startup, decoding and companion checks remain enabled.

```powershell
msbuild Desktop_Gremlin.sln /m /p:Configuration=Release
dotnet run --project tests/DesktopGremlin.WindowsTests -c Release
```

This repairs a reproduced resource-lifetime defect found during source review; it does not claim to diagnose unrelated upstream reports. Existing downloaded executables need rebuilding to gain the cleanup behavior.
