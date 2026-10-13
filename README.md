# NexusStrap

NexusStrap is an open-source Roblox launcher and bootstrapper for Windows.
It installs and updates the Roblox client and gives you control over client
settings, launch behavior, and appearance.

Unofficial community project. Not affiliated with or endorsed by Roblox
Corporation. Use at your own risk.

## Install

1. Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).
2. Download `NexusStrap.exe` from the
   [Releases page](https://github.com/susmoon81-netizen/NexusStrap/releases).
3. Run it.

## What it does

- Installs, updates, and launches Roblox Player and Studio
- FastFlag presets, templates, profiles, and a full flag editor
- One-click performance boost, frame-rate options, process priority
- Optional in-game FPS / CPU / RAM overlay
- Themes, custom fonts, backgrounds, icons, cursors, and sounds
- Multi-account switching, friends list with presence and join, profile pages
- Game browser with Continue, Discover, and Favorites
- Region-based server search with optional region pinning
- Discord Rich Presence for Player and Studio
- Game history, server history, desktop shortcuts

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/susmoon81-netizen/NexusStrap.git
cd NexusStrap
dotnet build NexusStrap.slnx -c Release
```

## Notes

- This repository contains source code only. Binaries ship through GitHub
  Releases, never in git history.
- NexusStrap never asks for your Roblox password. Never share your
  `.ROBLOSECURITY` cookie with anyone.

## License

See [LICENSE](LICENSE), [LICENSE-MIT](LICENSE-MIT), and
[LICENSE-UNLICENSE](LICENSE-UNLICENSE).
