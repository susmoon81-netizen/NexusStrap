# AGENTS.md — NexusStrap

This file captures high-signal guidance for agents working on this repo.

## Quick commands

- **Build:** `dotnet build NexusStrap.slnx -c Release` (CI uses the csproj directly)
- **Publish local dev:** `dotnet publish NexusStrap/NexusStrap.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -p:PublishReadyToRun=false`
- **Publish release:** `dotnet publish NexusStrap/NexusStrap.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:PublishTrimmed=false`
- **Copy exe:** `Copy-Item "NexusStrap\bin\Release\net10.0-windows\win-x64\publish\NexusStrap.exe" "NexusStrap.exe"`

## Git pitfalls

- **`git checkout -f main` overwrites all local changes.** If you need to switch branches, first stash or commit your work.
- The local copy was not a git repo (`no .git`); we initialized one and merged the safety backup branch.
- Always verify `git status --short` after git operations.

## Key project structure

- **Entry point:** `NexusStrap/App.xaml.cs` — installer detection, self-heal, launch args routing
- **Bootstrapper:** `NexusStrap/Bootstrapper.cs` (~2072 lines) — Roblox install/launch flow, mutex, CDN downloads, FastZip extraction
- **Settings UI:** `NexusStrap/UI/Elements/Settings/MainWindow.xaml` — Wpf.Ui NavigationCompact sidebar with hidden pages
- **Account Manager:** Separate WPF window; nav moved to right side
- **Watcher mode:** `NexusStrap.exe -watcher <base64 WatcherData>` — tray icon, RPC, crash recovery
- **Mods system:** Uses `Paths.Modifications` (not `Paths.PresetModifications`)
- **FFlags:** `ClientAppSettings.json` force-copied to `_latestVersionDirectory`
- **Cursor types:** `NexusStrap/Enums/CursorType.cs` + resources embedded in `NexusStrap.csproj`

## Build / publish quirks

- **Framework-dependent only:** csproj and Publish-x64.pubxml both hardcode `SelfContained=false` and `PublishReadyToRun=false`. Never publish self-contained — the apphost prompts the user to install the .NET 10 Desktop Runtime instead of bundling it.
- Must publish **without** `-o` output flag, then copy the exe manually, or MC3074 XAML error occurs.
- `dotnet build` must succeed before `dotnet publish`.

## Important constraints

- **Settings path:** Mods system uses `Paths.Modifications` (corrected from `Paths.PresetModifications`)
- **Shortcut creation:** `Utility/Shortcut.cs` uses ShellLink COM; shortcuts are framework-dependent
- **Navigation:** Hidden pages use `_alwaysHiddenTags` in `MainWindow.xaml.cs` (currently `fastflageditor` + `fflagtemplates`); Performance Boost lives as a card inside the Bootstrapper page, not a separate page
- **NaN crash:** `PositionHighlight` uses `ActualWidth`/`ActualHeight` with NaN fallbacks
- **Bootstrapper close:** Uses `Dispatcher.InvokeAsync` to marshal to UI thread before closing

## Code-modifying gotchas

- **`MainWindow.xaml.cs` `_alwaysHiddenTags`:** `"fastflageditor"` and `"fflagtemplates"` are hard-hidden (templates moved inside FastFlags page); everything else is searchable
- **XAML name scope:** `x:Name` must be on the `OptionControl` element itself (MC3093)
- **Cursor resources:** PNGs must be added to `NexusStrap.csproj` as `EmbeddedResource`
- **Version scan:** `_latestVersionDirectory` found by scanning `%LocalAppData%\Roblox\Versions\` for `version-*` folders