<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=A%20Game%20About%20Chopping%20Trees&fontColor=ffffff&fontSize=36&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unreal-5.8-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-not%20needed-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>A Game About Chopping Trees</i>.</p>

> **Single-player only.** The game has online co-op. As soon as another player is in your session, every cheat switches off by itself.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 2 minutes. You do **not** need a mod loader – everything is in the zip.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **A Game About Chopping Trees** → **Manage** → **Browse local files**.
3. A folder opens. In it you see the folders `Engine` and `ChoppingTrees`. **Keep this folder open.**

### Step 2 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-ChoppingTrees-v1.0.0.zip`** (always take the newest version).
2. Open the zip and copy the `ChoppingTrees` folder in it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: in `ChoppingTrees\Binaries\Win64\`, next to the `…-Win64-Shipping.exe`, there are now
   the file `dxgi.dll` and the folder `EvilDragon`.

### Step 3 – Play

Start the game, load a **single-player** save and press **F1**. The menu opens on the left side.
The menu loads about 10 seconds after the game starts.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | **God mode** (falling trees and logs do not hurt), **infinite stamina**, heal, walk speed up to 5x, jump height up to 4x, **fly mode** (through trees and the ground) with adjustable speed |
| **Money** | Add / remove money (presets or your own amount), **log value** up to 20x |
| **Chopping** | **One hit** (trees, stumps and logs break with one hit), chop speed up to 5x, **unlimited log capacity**, empty the log bag |
| **Upgrades** | Buy every upgrade one by one or **max everything at once** – free, your money stays. Unlock all skins |
| **Spawn** | **Spawn logs** (1–100, mixed or one kind of tree) – **in front of you** or straight into your log bag. **Regrow every tree** |
| **Teleport** | Sawmill, trolley, trailer, bench, notebook, your dog, **save 3 positions** |
| **Settings** | 6 themes, change keys, notification sound on / off, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |

You can change all keys in the **Settings** tab. While the menu is open your character stands still.

**Fly mode:** WASD to fly, Space goes up, Ctrl or C goes down, Shift is faster.

**Notifications:** every action shows a short glass card in the bottom-left corner with a soft click. You can turn the sound off in the *Settings* tab.

**Save Settings:** in the *Settings* tab. All cheats you have switched on are back the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| F1 does nothing | Check that `dxgi.dll` is directly next to the `…-Win64-Shipping.exe` (in `ChoppingTrees\Binaries\Win64\`, **not** at the top of the game folder) with `EvilDragon\EvilDragon.ChoppingTreesCheats.dll` beside it. Wait about 10 seconds after the game starts. |
| I use ReShade | ReShade is often called `dxgi.dll` too. Rename ReShade to `d3d12.dll`. |
| My antivirus flags the DLL | That is a false alarm because the mod loads itself into the game. The full source code is in this repository. |
| The menu says the cheats are off | Other players are in your session – play alone. Or you are still in the main menu – load a save first. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `EvilDragon\EvilDragon.ChoppingTreesCheats.log`. |

## Uninstall

Delete the file `dxgi.dll` and the folder `EvilDragon` in `ChoppingTrees\Binaries\Win64\`.

## Notes

- Money, bought upgrades and sold logs stay in your save.
- Spawned logs are normal log pieces – you can pick them up, carry them and sell them at the saw.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `EvilDragon\EvilDragon.ChoppingTreesCheats.ini`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

A Game About Chopping Trees is an Unreal Engine 5.8 game, so the mod is written in C++.

Requirements: Visual Studio with the C++ tools, Python 3, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook)
and an SDK of the game made with [Dumper-7](https://github.com/Encryqed/Dumper-7). For UE 5.8 Dumper-7 needs the fix from PR #594 (FFieldClass layout).

1. `shared\unreal\loader\build.bat` builds the `dxgi.dll` (loads every DLL from the `EvilDragon` folder).
2. Load Dumper-7 into the game once (put the DLL into the `EvilDragon` folder, start the game) – the SDK lands in `C:\Dumper-7`.
3. Copy `src\local.bat.example` to `src\local.bat` and fill in the paths.
4. Run `src\build.bat`. `shared\unreal\fix_sdk.py` repairs known Dumper-7 SDK problems automatically.
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
