# BUGrock Launcher (BETA !)

[![Latest Release](https://img.shields.io/github/v/release/7AmoZ/BUGrock-Launcher?style=for-the-badge&label=Download%20Latest%20Release&color=38a169)](https://github.com/7AmoZ/BUGrock-Launcher/releases/latest)
[![Direct Download](https://img.shields.io/badge/Direct%20Download-Windows%20x64%20(.zip)-2b6cb0?style=for-the-badge&logo=windows)](https://github.com/7AmoZ/BUGrock-Launcher/releases/latest/download/BUGrock_Launcher_v1.0.1_win-x64.zip)
[![Project Status: Public Beta](https://img.shields.io/badge/Status-Public%20Beta-f59e0b?style=for-the-badge&logo=git)](https://github.com/7AmoZ/BUGrock-Launcher)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=for-the-badge)](LICENSE.txt)

> [!IMPORTANT]
> **Project Status: Public Beta**
> BUGrock Launcher is currently in active Public Beta. Core features (side-by-side versions, GDK & UWP package staging, offline sideloading, and world preservation) are fully operational, but you may occasionally encounter minor bugs or UI quirks. We welcome your feedback, suggestions, and [issue reports](https://github.com/7AmoZ/BUGrock-Launcher/issues)!

A modern, feature-rich custom launcher for **Minecraft: Bedrock Edition** on Windows 10 & Windows 11, supporting both modern **GDK (`.msixvc`)** and legacy **UWP (`.appx`)** builds side-by-side.

> ### 📥 **[Click Here to Download BUGrock Launcher (Windows x64)](https://github.com/7AmoZ/BUGrock-Launcher/releases/latest)**

---

## ✨ Features

- **Side-by-Side Installations**: Install and manage multiple **Release**, **Preview**, **Beta**, and **Imported** versions of Minecraft Bedrock without losing your worlds or reinstalling from scratch.
- **Quick Play & Instant Version Switcher**: Switch between installed versions or local `.msixvc` / `.appx` packages directly from the **Play** tab with one click (supports automatic upgrading, downgrading, and live cancellation).
- **GDK (`.msixvc`) & UWP (`.appx`) Support**:
  - Full staging, extraction, and manifest patching for modern GDK versions (`1.21+`).
  - Automatic background GDK decryption key check (`Auto-fetch decryption keys`) and PE header verification.
  - Full support for legacy UWP `.appx` versions.
- **World Preservation & Conflict Resolution**: Safeguards your Minecraft saves from deletion when uninstalling or switching UWP packages via temporary safe-holding, and detects multiple save paths across UWP (`LocalState`) and GDK (`Minecraft Bedrock\Users`) with an interactive conflict resolution dialog (plus a built-in "Find my data" scanner). Note: This protects saves during package operations and does not create automated periodic archives.
- **Local Package Sideloading & Offline Import**: Directly import and register local `.msixvc` and `.appx` packages from your storage with one click, enabling full offline installs and custom build testing.

---

## ⚙️ Quick System & Service Checklist

Before launching, make sure your PC meets the following requirements:

### 1. Essential Runtimes
- [x] **[.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** — Required to open the launcher GUI.
- [x] **[Visual C++ 2015–2022 x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)** — Required for C++ helper shims and Minecraft Bedrock engine.
- [x] **GPU Driver (DirectX 11+)** — Your graphics card must support DirectX 11 Feature Level 11_0 (Intel HD 4000+, Nvidia GTX 400+, or AMD HD 7000+).

### 2. Windows Settings
- [x] **Developer Mode: ON** — Go to `Settings -> System (or Privacy & Security) -> For developers` and toggle **Developer Mode** to **ON** (needed to register sideloaded packages).
- [x] **Short Path Folder** — Extract the launcher to a short path like `C:\BUGrock` or `D:\Games\BUGrock` (avoids Windows 260-character MAX_PATH launch errors).

### 3. Critical Windows Services (Run `services.msc`)
Make sure these services are **NOT disabled** (leave them on **Automatic** or **Manual**):
- **`AppXSvc` (AppX Deployment Service)** — Required to install and register packages.
- **`ClipSvc` (Client License Service)** — Required to decrypt modern GDK versions using your Store license.
- **`GamingServices` (Gaming Services)** — Required for modern Bedrock versions & protocol activation.
- **`wuauserv` (Windows Update)** — Required to fetch official download packages from Microsoft servers.

> [!WARNING]
> **Windows "Lite" / "Ghost" Editions**: If you use a stripped-down custom Windows ISO with Microsoft Store or Xbox services removed, you must reinstall the **Microsoft Store** and **Xbox App** for the launcher to function.

---

## 🚀 Setup & Usage

1. Download **`BUGrock_Launcher_v1.0.1_win-x64.zip`** from [Releases](https://github.com/7AmoZ/BUGrock-Launcher/releases/latest).
2. Extract the `.zip` archive to a folder with a short path (e.g. `C:\Games\BUGrockLauncher`).
3. Run **`BUGrock_Launcher.exe`**.
4. In the **Installations** tab, download any version or click **Import** to select local `.msixvc` / `.appx` files.
5. In the **Play** tab, pick your version and hit **PLAY**.

---

## 🔧 Quick Troubleshooting

| Issue | Quick Fix |
| :--- | :--- |
| **`Minecraft.Windows.exe is encrypted`** | Make sure your Microsoft account owns the game in Microsoft Store. Start downloading Minecraft from the Store for 3 seconds to let Windows register the decryption key, cancel the Store download, and retry in BUGrock Launcher. |
| **`Invalid argument` / launch error** | The launcher path is too long. Move the launcher to a shorter path (e.g., `C:\BUGrock`) and launch once from inside the launcher. |
| **Cannot run two versions simultaneously** | Windows allows only one active Minecraft Bedrock process at a time. Close the running version before launching another. |
| **`Unable to fetch download URL` (Beta)** | Beta versions require your account to be enrolled in the Minecraft Beta program via the **Xbox Insider Hub** app. |
| **Launcher closes immediately on startup** | You are missing [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). Install it and reopen. |

---

## 🛠️ Building from Source

### Requirements
- **Visual Studio 2022** (or newer) with `.NET desktop development` (.NET 8.0), `Desktop development with C++` (MSVC v143), and Windows 10/11 SDK.
- **NuGet CLI** (for native C++/WinRT package restore).

```powershell
nuget restore BUGrockLauncher.sln
msbuild BUGrockLauncher.sln /p:Configuration=Release /p:Platform=x64 /p:RestorePackagesConfig=true
```
The compiled binaries will be placed in `x64\Release\`.

---

## 📄 License & Legal Notice

**BUGrock Launcher** is free and open-source software licensed under the **GNU General Public License v3.0 (GPL-3.0)** — see the [LICENSE.txt](LICENSE.txt) file for the full license text.

- **Copyright © 2026 [7AmoZ](https://github.com/7AmoZ)** — UI redesign, Quick Play & version switcher, 3D skin viewer, GDK decryption key automation, codebase refactoring, and security/stability fixes (modified 2026).
- **Third-Party Libraries**: Includes [TinyXML-2](https://github.com/leethomason/tinyxml2) under the zlib License, and [Newtonsoft.Json](https://www.newtonsoft.com/json) under the MIT License.
- **Trademark Disclaimer**: **NOT AN OFFICIAL MINECRAFT PRODUCT. NOT APPROVED BY OR ASSOCIATED WITH MOJANG OR MICROSOFT.** *Minecraft* is a registered trademark of Mojang Synergies AB / Microsoft Corporation. All game packages and assets remain the property of their respective owners and require a valid Microsoft Store license.

---

###### Credits & Upstream Attribution
<sub>In compliance with Section 5 of the GNU General Public License v3.0, this project is a heavily modified and refactored derivative work originally based on the open-source <a href="https://github.com/MCMrARM/mc-w10-version-launcher">mc-w10-version-launcher</a> / <a href="https://github.com/dktapps-pm-pl/mc-w10-version-launcher">MCLauncher</a> project by MCMrARM, dktapps, and contributors, licensed under GPL-3.0.</sub>
