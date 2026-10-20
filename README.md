# BUGrock Launcher

A modern, feature-rich custom launcher for **Minecraft: Bedrock Edition** on Windows 10 & Windows 11, supporting both modern **GDK (`.msixvc`)** and legacy **UWP (`.appx`)** builds side-by-side.

---

## ✨ Features

- **Side-by-Side Installations**: Install and manage multiple **Release**, **Preview**, **Beta**, and **Imported** versions of Minecraft Bedrock without losing your worlds or reinstalling from scratch.
- **Quick Play & Instant Version Switcher**: Switch between installed versions or local `.msixvc` / `.appx` packages directly from the **Play** tab with one click (supports automatic upgrading, downgrading, and live cancellation).
- **GDK (`.msixvc`) & UWP (`.appx`) Support**:
  - Full staging, extraction, and manifest patching for modern GDK versions (`1.21+`).
  - Automatic background GDK decryption key check (`Auto-fetch decryption keys`) and PE header verification.
  - Full support for legacy UWP `.appx` versions.
- **Interactive 3D Skin Preview**: Built-in 3D character head viewport in the sidebar that tracks mouse movement, with support for uploading custom `.png` skins and switching presets.
- **Smart World Backup & Migration**: Automatically detects, backs up, and restores Minecraft world data across UWP (`LocalState`) and GDK (`Minecraft Bedrock\Users`) directories with an interactive conflict resolution dialog.
- **Custom Installation Paths & Live Speed Meter**: Choose custom install directories per version and monitor real-time download speeds (`MB/s` / `KB/s`) and progress percentages.

---

## 📋 Prerequisites

> [!IMPORTANT]
> **BUGrock Launcher does NOT bypass game ownership.** You must be signed into the Microsoft Store with a Microsoft account that owns **Minecraft for Windows** (or Xbox Game Pass).

1. **Windows 10 / Windows 11 (64-bit)**.
2. **Valid Minecraft License**: Signed into Microsoft Store with an account that owns Minecraft: Bedrock Edition.
   - *Note*: For **GDK (`.msixvc`)** versions, having installed Minecraft (or Minecraft Preview) from the Microsoft Store at least once on your PC ensures Windows registers the system decryption keys (`ClipSvc`).
3. **Developer Mode Enabled**:
   - Open **Windows Settings** $\rightarrow$ **System** (or **Privacy & Security**) $\rightarrow$ **For developers** $\rightarrow$ Enable **Developer Mode**.
4. **Runtimes**:
   - [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
   - [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)
5. *(Optional)* **Xbox Insider Hub**: Required only if you wish to download legacy UWP **Beta** builds.

---

## 🚀 Setup & Usage

1. Download the latest build from the [Releases](https://github.com/7AmoZ/BUGrock-Launcher/releases) page (or Actions artifacts) and extract the archive to a folder with a short path (e.g., `C:\Games\BUGrockLauncher`).
2. Run **`BUGrock_Launcher.exe`**.
3. Use the **Installations** tab to download any Release, Preview, or Beta version, or click **Import** to install a local `.msixvc` or `.appx` package.
4. Use the **Play** tab to select your active version and launch the game.

---

## 🛠️ Building from Source

### Requirements
- **Visual Studio 2022** (or newer) with:
  - **.NET desktop development** workload (`.NET 8.0 SDK`)
  - **Desktop development with C++** workload (`MSVC v143`)
  - **Windows 10/11 SDK** (`10.0.17763.0` or newer)
- **NuGet CLI** (for restoring native C++/WinRT packages in `WUTokenHelper`).

### Build Commands
```powershell
nuget restore BUGrockLauncher.sln
msbuild BUGrockLauncher.sln /p:Configuration=Release /p:Platform=x64 /p:RestorePackagesConfig=true
```
The compiled binaries will be placed in `x64\Release\`.

---

## 🔧 Troubleshooting

### `Minecraft.Windows.exe is encrypted` / Decryption Keys Required
Modern GDK versions (`.msixvc`) use Windows EFS / `ClipSvc` encryption tied to your Microsoft Store license.
- Ensure **Auto-fetch decryption keys** is enabled under **Options** in the top bar.
- If the system decryption key is missing, start downloading **Minecraft** (or **Minecraft Preview**) from the **Microsoft Store** for a few seconds until it begins acquiring the license/downloading, then cancel it in the Store and retry in **BUGrock Launcher**.
- You can also run **Tools $\rightarrow$ Cleanup for installing Minecraft from Microsoft Store** if an existing development-mode registration blocks the Store.

### `Invalid argument` when launching via Start Menu
Move the launcher folder to a shorter path (such as `C:\BUGrockLauncher`) and launch the version at least once from inside the launcher.

### Running Multiple Instances Simultaneously
Because all Bedrock Release builds share the same Windows package family (`Microsoft.MinecraftUWP_8wekyb3d8bbwe`), Windows only allows one active registration per package family at a time. You can keep as many versions installed side-by-side as you like and switch between them before launching.

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)** — see the [LICENSE.txt](LICENSE.txt) file for details.
