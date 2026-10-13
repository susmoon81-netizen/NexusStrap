<div align="center">

# NexusStrap

**Advanced Roblox Bootstrapper & Launcher for Windows**

[Latest release](https://github.com/susmoon81-netizen/NexusStrap/releases/latest) | [Website](https://nexusstrap.pages.dev/) | [Discord](https://discord.gg/R47Pwf7s9q)

</div>

---

<div align="center">

[![downloads](https://img.shields.io/github/downloads/susmoon81-netizen/NexusStrap/total?style=for-the-badge&logo=github&color=5e3a94&label=TOTAL%20DOWNLOADS)](https://github.com/susmoon81-netizen/NexusStrap/releases)
[![downloads-latest](https://img.shields.io/github/downloads/susmoon81-netizen/NexusStrap/latest/total?style=for-the-badge&logo=github&color=a855f7&label=LATEST%20RELEASE)](https://github.com/susmoon81-netizen/NexusStrap/releases/latest)
[![release](https://img.shields.io/github/v/release/susmoon81-netizen/NexusStrap?style=for-the-badge&logo=github&color=2563eb&label=VERSION)](https://github.com/susmoon81-netizen/NexusStrap/releases)
[![stars](https://img.shields.io/github/stars/susmoon81-netizen/NexusStrap?style=for-the-badge&logo=github&color=6b7280&label=STARS)](https://github.com/susmoon81-netizen/NexusStrap/stargazers)

<!-- Note: Discord online count requires a bot or manual update as GitHub doesn't host Discord data -->
[![discord](https://img.shields.io/discord/1534049669418586232?style=for-the-badge&logo=discord&color=4f545c&label=DISCORD)](https://discord.gg/R47Pwf7s9q)

</div>

> **Disclaimer:** This is an unofficial community project. It is not affiliated with, endorsed by, or sponsored by Roblox Corporation. Use at your own risk.

---

## Overview

**NexusStrap** is a powerful, open-source bootstrapper designed to give users full control over the Roblox client on Windows. It handles installation, updates, and launching while providing extensive customization options for performance tuning, visual appearance, and account management.

### Key Capabilities

*   **Bootstrapping:** Automatically installs, updates, and launches Roblox Player and Studio.
*   **Performance Tuning:** Full FastFlag editor with presets, templates, and profiles. Includes one-click performance boosts, frame-rate controls, and process priority settings.
*   **Overlay:** Optional in-game HUD displaying FPS, CPU, and RAM usage.
*   **Customization:** Deep theming engine supporting custom fonts, backgrounds, icons, cursors, and sounds.
*   **Account Management:** Multi-account switching, friends list with presence/join, and detailed profile pages.
*   **Game Browser:** Integrated discovery with Continue, Discover, and Favorites tabs.
*   **Server Tools:** Region-based server search with optional region pinning.
*   **Social:** Discord Rich Presence for both Player and Studio.
*   **History:** Tracks game and server history, plus generates desktop shortcuts.

---

## Installation

### Prerequisites
*   **OS:** Windows 10 or later (64-bit)
*   **Runtime:** [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) is **required**.

### Steps
1.  Download the latest `NexusStrap.exe` from the [Releases page](https://github.com/susmoon81-netizen/NexusStrap/releases).
2.  Ensure the .NET 10 Desktop Runtime is installed.
3.  Run `NexusStrap.exe`. No installation wizard is needed; it is portable.

---

## Build From Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/susmoon81-netizen/NexusStrap.git
cd NexusStrap
dotnet build NexusStrap.slnx -c Release
