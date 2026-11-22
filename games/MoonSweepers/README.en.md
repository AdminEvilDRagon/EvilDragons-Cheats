<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Moon%20Sweepers&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6000.6%20IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-not%20needed-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Moon Sweepers</i>.</p>

> **Single-player only.** Moon Sweepers has online co-op. As soon as another player is in your crew, every cheat switches off by itself.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 2 minutes. You do **not** need a mod loader (no BepInEx either) – everything is in the zip.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Moon Sweepers** → **Manage** → **Browse local files**.
3. A folder opens. In it you see the file `MoonSweepers.exe`. **Keep this folder open.**

### Step 2 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-MoonSweepers-v1.0.0.zip`** (always take the newest version).
2. Open the zip and copy **everything in it** (the file `dxgi.dll` and the folder `EvilDragon`) into your game folder.
3. Check: next to `MoonSweepers.exe` there are now `dxgi.dll` and the folder `EvilDragon`.

### Step 3 – Play

Start the game, load a **single-player** save and press **F1**. The menu opens on the left side.
The menu loads about 10 seconds after the main menu.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Walk speed up to 5x, jump height up to 4x, **fly mode with noclip** (through rocks, buildings and the ground) with adjustable speed, **jetpack with 99 boosts** |
| **Money** | Add / remove money (presets or your own amount), **payout per kg** up to 20x |
| **Dust** | **Fill** / empty your dust bag, **endless dust**, bag up to 20x bigger, **clean the sections around you instantly** (3 sizes) |
| **Upgrades** | Buy every tool, every container, the whole skill tree and the gear – **max everything at once** (free) or **free upgrades** |
| **Spawn** | **Dust capsules** (1, 5 or 10 full capsules in front of you), **capsule catcher**, **robot docks** with their robot |
| **Teleport** | Back to the depot, every station, garage, upgrade terminal, supply wall, locker, map, kitchen, lounge, robot docks, landmarks, **save 3 positions** |
| **Settings** | 6 themes, change keys, notification sound on / off, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |
| <kbd>F4</kbd> | Fill your dust bag |

You can change all keys in the **Settings** tab. While the menu is open the camera stands still and your mouse is free.

**Fly mode:** the game's own fly mode – you fly through everything.

**Notifications:** every action shows a short glass card in the bottom-left corner with a soft click. You can turn the sound off in the *Settings* tab.

**Save Settings:** in the *Settings* tab. All cheats you have switched on are back the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| F1 does nothing | Check that `dxgi.dll` is directly next to `MoonSweepers.exe` with `EvilDragon\EvilDragon.MoonSweepersCheats.dll` beside it. Wait about 10 seconds after the main menu. |
| I installed BepInEx | BepInEx 6 does not run on this Unity version (6000.6) yet and is not needed for this mod. Rename `winhttp.dll` in the game folder to `winhttp.dll.off`. |
| I use ReShade | ReShade is often called `dxgi.dll` too. Rename ReShade to `d3d12.dll`. |
| My antivirus flags the DLL | That is a false alarm because the mod loads itself into the game. The full source code is in this repository. |
| The menu says the cheats are off | Other players are in your crew – play alone. Or you are still in the main menu – load a save first. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `EvilDragon\EvilDragon.MoonSweepersCheats.log`. |

## Uninstall

Delete the file `dxgi.dll` and the folder `EvilDragon` from the game folder.

## Notes

- Money, bought upgrades, delivered dust and cleaned sections stay in your save.
- The mod uses the game's built-in developer helpers (money, dust, robots, fly mode) and only unlocks them for its own actions.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `EvilDragon\EvilDragon.MoonSweepersCheats.ini`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Moon Sweepers runs on Unity 6000.6 (IL2CPP metadata v108). BepInEx 6 and Il2CppInterop do not support this version yet,
so the mod is a native C++ DLL: it finds classes and methods through the `il2cpp_*` exports of `GameAssembly.dll`
(they stay the same between Unity versions) and uses the ImGui overlay and the loader from `shared/unreal`.

Requirements: Visual Studio with the C++ tools, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook).

1. `shared\unreal\loader\build.bat` builds the `dxgi.dll` (loads every DLL from the `EvilDragon` folder).
2. Copy `src\local.bat.example` to `src\local.bat` and fill in the paths.
3. Run `src\build.bat`.
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
