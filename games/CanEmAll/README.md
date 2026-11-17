<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Can%20%27Em%20All&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-2.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6%20IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10&logo=unity" />
  <img src="https://img.shields.io/badge/BepInEx-6.0.0--be.793-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10&logo=windows" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – a clean in-game cheat menu for <i>Can 'Em All</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## ✨ Features

| Tab | Cheats |
|:--|:--|
| 🧍 **Player** | God mode · One-hit kill · Infinite ammo (shows **∞** in the HUD) · Auto kill · Infinite stamina · Walk speed up to 10x · Game speed 0.25x–5x · Freeze day timer · Kill all |
| 💰 **Money** | Add / remove / set money (presets or custom amount) · Income multiplier up to x100 · Rebirth points |
| 🏭 **Factory** | Turbo machines (20x faster processing) · Conveyor x4 · All cans shiny · Extra cans per enemy · Turrets infinite ammo · Turn on all machines · Unlock all skills |
| 🐲 **Spawn** | Spawn any creature (1–1000, shiny optional) at your crosshair or in front of you · Spawn cans with any value · Teleport creatures, a single creature type, cans or everything to your crosshair / to you |
| ⚙️ **Settings** | 6 themes · Rebind hotkeys · Save Settings (loaded automatically next start) |

## ⌨️ Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close menu |
| <kbd>K</kbd> | Kill all enemies |
| <kbd>T</kbd> | Teleport all creatures & cans (to crosshair or to you) |
| <kbd>J</kbd> | Spawn the selected creature |

All hotkeys can be changed in the **Settings** tab. While the menu is open, the game ignores mouse clicks, so you don't shoot while clicking buttons.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## 📥 Install

1. Download **BepInEx 6 (Unity IL2CPP, win-x64)** bleeding-edge build **be.793** or newer from <https://builds.bepinex.dev/projects/bepinex_be>.
2. Extract it into the game folder (where `Can 'Em All.exe` is).
3. Start the game **once** and wait until the main menu appears (the first start takes 1–3 minutes), then close it.
4. Download `EvilDragon.CanEmAllCheats.dll` from the [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and put it into `BepInEx\plugins\`.
5. Start the game and press **F1**.

**Uninstall:** delete `EvilDragon.CanEmAllCheats.dll`. To remove BepInEx completely, also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folders `BepInEx` and `dotnet`.

## 🛠️ Build from source

Requirements: .NET SDK 6.0 or newer, the game with BepInEx installed and started once (so `BepInEx\interop` exists).

```powershell
cd games/CanEmAll/src
copy GameDir.props.example GameDir.props   # then put your game folder in it
dotnet build -c Release
```

The DLL is copied to `BepInEx\plugins` automatically (unless the game is running).

## 📝 Notes

- Single-player only. Nothing is changed until you switch it on.
- Steam achievements may still unlock while cheats are active. Keep that in mind if you care about them.
- Settings are stored in `BepInEx\config\evildragon.cheats.canemall.cfg`.

## 📜 Changelog

**2.0.0**
- New name: EvilDragon's Cheats, with logo
- 6 themes (Dragon, Neon, Inferno, Ocean, Purple, Gold)
- *Save Settings* – toggles and values are restored on the next start
- Fixed: rocket launcher kept reloading forever with infinite ammo
- English UI, readable bold fonts, hotkey bar

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
