# Character selection at startup

This implements the startup-selection requests in upstream [#70](https://github.com/Kritzkingvoid/Desktop_Gremlin/issues/70) and the random-start portion of [#88](https://github.com/Kritzkingvoid/Desktop_Gremlin/issues/88). Previously the app loaded only `START_CHAR` and ignored startup arguments, so separate shortcuts could not select different installed characters without editing or duplicating the installation.

## Separate shortcuts

Use the exact folder name under `SpriteSheet\Gremlins`. For example, if Cafe and Rice Shower are installed, Windows shortcut **Target** fields can be:

```text
"C:\Games\DesktopGremlin\DesktopGremlin.exe" --character "Cafe"
"C:\Games\DesktopGremlin\DesktopGremlin.exe" --character "Rice Shower"
```

The quoted name must match the real installed folder; the examples do not download or install characters. Both shortcuts can run concurrently from one installation. Character-specific config is loaded after selection, so each process uses its selected pack's scale, geometry, animations, sounds and icon. Shared master settings and the companion selection still come from the installation's `config.txt`.

The tray's **Restart** command retains an explicit shortcut choice, including names containing spaces. Paths are resolved from the executable's directory, so a shortcut does not need a particular **Start in** directory to locate the character config/sheets/audio. The tray icon now also resolves from the installation, with the existing application-icon fallback if the pack has none.

## Random startup

Opt in persistently in the main `config.txt`:

```ini
START_CHAR = Cafe
RANDOM_CHARACTER = true
```

Or opt in for one launch:

```text
"C:\Games\DesktopGremlin\DesktopGremlin.exe" --random-character
```

The default is `RANDOM_CHARACTER = false`. Random selection shuffles installed main-character folders under `SpriteSheet\Gremlins`, then takes the first pack whose required config and idle sprite pass the production validation. Each candidate is tried at most once. Missing/malformed config, corrupt/missing idle PNGs and frame-count/layout mismatches are skipped and logged to `%LOCALAPPDATA%\DesktopGremlin\diagnostics.log`. Empty installations or all-broken candidates produce a clear startup error and exit code 1, rather than an endless retry or a silent unrelated selection.

A random launch can choose the same character again. There is no persisted history or guarantee of a different result each time. Companion packs are not candidates. Required idle validation does not promise that every optional animation/sound exists; PR #2's normal fallback behavior still handles incomplete optional artwork.

## Precedence and errors

1. `--character "Folder name"` selects that character, even if either random option is enabled.
2. Otherwise, `--random-character` or `RANDOM_CHARACTER=true` selects an installed usable pack randomly.
3. Otherwise, `START_CHAR` retains its existing behavior.

An explicit missing/broken pack fails through the normal required-pack diagnostic; it never silently falls back to a random pack. Selection does not rewrite either the main config or pack configs. Folder names may contain spaces/Unicode but must be a single name: absolute paths, traversal, path separators, control characters and Windows-invalid filename characters are rejected. Unknown arguments, duplicate switches and a missing `--character` value produce a usage diagnostic and exit code 1. `ALLOW_ERROR_MESSAGES=false` suppresses dialogs while retaining diagnostics and the exit code.

Random validation uses a private sprite store, so rejected candidates cannot change the selected character, scales/geometry or active renderer cache. The selected pack is then loaded normally. Restart preserves the startup policy: explicit choices remain explicit; random launches choose again.

## Validation and limits

Portable tests exercise default/explicit/random precedence, spaced names and restart quoting, invalid arguments/paths, missing/empty/all-broken installations, bounded retries, reachability of both usable candidates across deterministic seeds, and boolean config parsing. They run on .NET 8 on Windows/Linux and .NET Framework 4.8 on Windows.

Windows WPF tests probe real PNG decoding and crop validation, confirm rejected candidates leave selection/scale/geometry unchanged, construct the main window with an explicit override above random mode, check restart arguments and unchanged saved config, then launch the real executable with invalid arguments and a missing explicit pack from another working directory. Those child processes must exit cleanly with the expected diagnostics.

Run the existing workflow or:

```powershell
dotnet run --project tests/DesktopGremlin.RegressionTests -c Release -f net8.0
dotnet run --project tests/DesktopGremlin.RegressionTests -c Release -f net48
msbuild Desktop_Gremlin.sln /m /p:Configuration=Release
dotnet run --project tests/DesktopGremlin.WindowsTests -c Release
```

This does not implement #88's separate song/jukebox request, create artwork, supply the missing Options GUI source, diagnose the blue-rendering report (#85), or change already published executables. Existing downloaded executables must be replaced with a rebuilt version to gain these startup options.
