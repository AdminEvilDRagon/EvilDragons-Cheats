<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Storage%20Hunter%20Simulator&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.2.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unreal-5.4-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-not%20needed-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Storage Hunter Simulator</i>.</p>

> **Single-player only.** Storage Hunter Simulator has online co-op. As soon as another player is in your session, every cheat switches itself off.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 2 minutes. You do **not** need a mod loader – everything is in the zip.

### Step 1 – Open the game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Storage Hunter Simulator** → **Manage** → **Browse local files**.
3. A folder opens. You can see the folders `Engine` and `StorageHunter`. **Keep this folder open.**

### Step 2 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-StorageHunter-v1.2.0.zip`** (always take the newest version).
2. Open the zip and copy the `StorageHunter` folder inside it into your game folder.
   If Windows asks to merge/replace, click **Yes**.
3. Check: `StorageHunter\Binaries\Win64\` now has the file `dxgi.dll` and the folder `EvilDragon`
   next to `StorageHunter-Win64-Shipping.exe`.

### Step 3 – Play

Start the game, load a **single-player** save and press **F1**. The menu opens on the left.
The menu loads about 10 seconds after the game starts.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Walk & sprint speed up to 5x, jump height, gravity, unlimited jumps, fly mode (through walls) with adjustable speed |
| **Money** | Add / remove money (presets or your own amount), renown & level per district, perk points, tickets |
| **Items** | Spawn any item – with categories, search, amount (1–20) and "already verified". **Special** category for the special items |
| **Workshop** | Tool levels (verification, fixing, lockpicking), verify & fix always succeed, unlock containers, random items |
| **Business** | Level up / buy the pawn shop, **win every auction** (NPC bidders have no money), complete task / quest, infinite fuel, fill the tank |
| **World** | **Teleport** to houses, pawn shops, auctions and the garage, **bring your car**, go to your car, save a position. **Unanchor**: make map objects loose (Ctrl + click or everything in range), push, freeze, put back. Set the time, freeze time |
| **Settings** | 6 themes, change keys, notification sound on / off, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |

You can change every key in the **Settings** tab. While the menu is open your character stands still.

**Fly mode:** WASD to fly, Space up, Ctrl or C down, Shift faster.

**Unanchor:** aim at an object and press **Ctrl + Left click** – it falls down. Ctrl + Left click again pushes it, **Ctrl + Right click** freezes it. With the menu open, the object under the mouse counts. Walls and floors only with "Big objects". Nothing of this is saved.

**Spawning items:** click an item and press **Spawn** – or double-click it. Items appear in front of you.

**Save Settings:** in the *Settings* tab. Every cheat you turned on is on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Fix |
|:--|:--|
| F1 does nothing | Make sure `dxgi.dll` is right next to `StorageHunter-Win64-Shipping.exe` (in `StorageHunter\Binaries\Win64\`, **not** next to `StorageHunter.exe` at the top) with `EvilDragon\EvilDragon.StorageHunterCheats.dll` beside it. Wait about 10 seconds after starting. |
| I use ReShade | ReShade is often called `dxgi.dll` too. Rename ReShade to `d3d12.dll`. |
| My antivirus flags the dll | That is a false alarm, because the mod loads itself into the game. The full source code is in this repository. |
| The menu says "cheats are disabled" | You are in an online session with other players. Play alone. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `EvilDragon\EvilDragon.StorageHunterCheats.log`. |

## Uninstall

Delete the file `dxgi.dll` and the folder `EvilDragon` in `StorageHunter\Binaries\Win64\`.

## Notes

- Money, renown, perk points and spawned items stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `EvilDragon\EvilDragon.StorageHunterCheats.ini`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Storage Hunter Simulator is an Unreal Engine game, so this mod is written in C++ (not C# like the Unity mods).

Requirements: Visual Studio with the C++ tools, Python 3, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook)
and an SDK of the game made with [Dumper-7](https://github.com/Encryqed/Dumper-7).

1. `shared\unreal\loader\build.bat` builds `dxgi.dll` (it loads every dll from the `EvilDragon` folder).
2. Load Dumper-7 into the game once (put its dll into the `EvilDragon` folder, start the game) – the SDK ends up in `C:\Dumper-7`.
3. Copy `src\local.bat.example` to `src\local.bat` and fill in your paths.
4. Run `src\build.bat`. `shared\unreal\fix_sdk.py` automatically repairs a bug in the Dumper-7 SDK.
</details>

## Changelog

**1.2.0**
- New glass notifications in the bottom-left corner with a soft click (sound can be turned off in *Settings*), errors in red

**1.1.0**
- Teleport to houses, pawn shops, auctions and the garage, save a position
- Bring your car / go to your car
- Unanchor: make objects loose with Ctrl + click or in range, push, freeze, put back
- The menu animates when opening and closing

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
