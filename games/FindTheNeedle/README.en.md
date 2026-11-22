<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Find%20The%20Needle&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Godot-4.7-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-not%20needed-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Find The Needle</i>.</p>

> **Unofficial mod.** Not made by the developer of Find The Needle, use at your own risk. You need the **full game** – the mod contains none of the game's files.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 2 minutes. You do **not** need a mod loader – everything is in the zip.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Find The Needle** → **Manage** → **Browse local files**.
3. A folder opens. In it you see the file `FindTheNeedle.exe`. **Keep this folder open.**

### Step 2 – Install the cheats

1. **Close the game.**
2. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-FindTheNeedle-v1.0.0.zip`** (always take the newest version).
3. Open the zip and copy **all three files** (`EvilDragon.FindTheNeedleCheats.dll`, `install.bat`, `uninstall.bat`) into your game folder.
4. Double-click **`install.bat`**. It says "Done!".

`install.bat` renames the game's terrain extension to `libterrain.windows.release.x86_64.original.dll` and puts the mod in its place. The mod loads the original itself – the terrain looks exactly like before.

### Step 3 – Play

Start the game, load a save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | **Infinite stamina**, **infinite jetpack**, **no fall death**, refill stamina & jetpack, walk speed up to 5x, **fly mode / noclip** (the game's own – through hay, walls and the ground) |
| **Money** | Add / remove money (presets or your own amount), **pay off your debt** |
| **Upgrades** | **All 8 tools** (one by one or all), the **whole tech tree** by branch – every upgrade one by one or **unlock everything at once** (free) |
| **Spawn** | **Spawn any item** of the game (1x, 5x or 10x, with search) – it lands in front of you. Wearables like the jetpack are unlocked right away |
| **Teleport** | To **every needle** still in the hay (with name and distance), to the key, to the yard start, **save 3 positions** |
| **Settings** | 6 themes, change keys, notification sound on / off, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |

You can change all keys in the **Settings** tab. While the menu is open your mouse is free.

**Notifications:** every action shows a short glass card in the bottom-left corner with a soft click. You can turn the sound off in the *Settings* tab.

**Save Settings:** in the *Settings* tab. The cheats you switched on are back the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| F1 does nothing | Run `install.bat` again (with the game closed). After a **game update** you always have to – Steam replaces the terrain file. |
| `install.bat` says the file is missing | Right-click the game in Steam → **Properties → Installed Files → Verify integrity of game files**, then run `install.bat` again. |
| My antivirus flags the DLL | That is a false alarm because the mod loads itself into the game. The full source code is in this repository. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `EvilDragon.FindTheNeedleCheats.log` from the game folder. |

## Uninstall

Double-click **`uninstall.bat`** (with the game closed) – the game's terrain file comes back. Then you can delete `EvilDragon.FindTheNeedleCheats.dll`, `install.bat` and `uninstall.bat`.

## Notes

- **Leaderboards:** as soon as you use a cheat, the mod marks the run as debug/modded through the game's own flags – cheat runs never land on the online leaderboards.
- Money, upgrades, tools and spawned items stay in your save.
- Your settings are stored in `EvilDragon.FindTheNeedleCheats.ini` in the game folder.

<details>
<summary><b>How the mod works / build from source</b> (for developers)</summary>

Find The Needle is a Godot 4.7 game with encrypted game data. The mod does not decrypt anything: it stands in for the
Terrain3D extension (`libterrain…dll`), passes its start-up on to the real DLL and so gets Godot's official GDExtension
interface. Through it, it reads and changes the game scripts' variables by name (`src/gd.h`) – on the game's main thread
via `register_main_loop_callbacks`. The menu is the ImGui overlay from `shared/unreal` (D3D12).

`src/probe.cpp` is a developer tool: it writes the game's structure (autoloads, scene, script variables) to text files.

Requirements: Visual Studio with the C++ tools, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook).

1. Copy `src\local.bat.example` to `src\local.bat` and fill in the paths.
2. Run `src\build.bat` and put `bin\libterrain.windows.release.x86_64.dll` next to `install.bat` as `EvilDragon.FindTheNeedleCheats.dll`.
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
