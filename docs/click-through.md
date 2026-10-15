# Click-through startup and controls

`CLICK_THROUGH=true` in the main `config.txt` makes the main gremlin window pass
mouse input through to windows underneath it. `false` keeps normal sprite and
hotspot interaction. The bundled config enables this setting.

The tray's **Toggle Click Through** checkmark reflects the current session state.
Use that menu to restore mouse interaction when the sprite cannot be clicked.
With `ENABLE_KEYBOARD=true` and the gremlin already focused, **0** also toggles
click-through. This is a focused-window shortcut, not a global hotkey.

Toggling affects the current session. It does not rewrite `config.txt`; change
`CLICK_THROUGH` there to choose the next launch's behavior. This setting applies
to the main gremlin window, not companion or food windows.

## Cause and fix

The master-config parser stored `CLICK_THROUGH`, but the window always started
with a separate `false` field and never applied the configured value. Only a
manual toggle changed the native extended style. That toggle could also call
Win32 with a zero handle if used before the window was shown, and the tray gave
no indication of the active mode.

The window now initializes its requested state from the loaded config and applies
it from WPF's `OnSourceInitialized`, when the native window handle exists. A
pre-handle toggle updates the requested state without forcing handle creation;
the final state is applied when WPF creates the handle. The same method handles
later toggles, preserves other extended style bits, and keeps `WS_EX_LAYERED`
while setting or clearing `WS_EX_TRANSPARENT`. The tray checkmark reads the same
window state after either menu or keyboard changes.

## Regression prevention

The Windows .NET Framework 4.8 runner exercises the production WPF window and
reads its actual native extended styles. It covers both config values, zero,
one and two toggles before handle creation, `EnsureHandle`, showing the window,
direct calls, the real keyboard handler, the tray menu handler, dispatcher
processing, unrelated native style bits, and preservation of the config file.
These checks run alongside the full WPF Release build and the existing portable
regressions on Windows/Linux .NET 8 and Windows .NET Framework 4.8.

For a manual Windows check, launch with each config value and click over the
sprite with another application behind it. With click-through enabled, that
application should receive the click. Disable it through the tray and verify
sprite interactions return. Repeat after moving to another display. Automated
style checks do not replace this physical input/display check.
