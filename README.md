<div align="center">

# NexusStrap

**Advanced Roblox Bootstrapper & Launcher for Windows**

[Latest release](https://github.com/susmoon81-netizen/NexusStrap/releases/latest) | [Website](https://your-website-link-here) | [Discord](https://discord.gg/your-invite-link)

</div>

---

<div align="center">

[![downloads](https://img.shields.io/badge/downloads-386k-purple)](https://github.com/susmoon81-netizen/NexusStrap/releases)
[![downloads-latest](https://img.shields.io/badge/downloads@latest-5.1k-violet)](https://github.com/susmoon81-netizen/NexusStrap/releases/latest)
[![release](https://img.shields.io/badge/release-v1.0.2-blue)](https://github.com/susmoon81-netizen/NexusStrap/releases/tag/v1.0.2)
[![discord](https://img.shields.io/badge/Discord-2.9k%20online-indigo)](https://discord.gg/your-invite-link)
[![stars](https://img.shields.io/badge/Stars-82-grey)](https://github.com/susmoon81-netizen/NexusStrap/stargazers)

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
