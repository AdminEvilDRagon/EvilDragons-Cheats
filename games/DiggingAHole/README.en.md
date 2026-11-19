<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=A%20Game%20About%20Digging%20A%20Hole&fontColor=ffffff&fontSize=36&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unreal-5.5-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-not%20needed-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>A Game About Digging A Hole</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 2 minutes. You do **not** need a mod loader – everything is in the zip.

### Step 1 – Open the game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **A Game About Digging A Hole** → **Manage** → **Browse local files**.
3. A folder opens. You can see the folders `Engine` and `DiggingGame`. **Keep this folder open.**

### Step 2 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-DiggingAHole-v1.0.0.zip`** (always take the newest version).
2. Open the zip and copy the `DiggingGame` folder inside it into your game folder.
   If Windows asks to merge/replace, click **Yes**.
3. Check: `DiggingGame\Binaries\Win64\` now has the file `dxgi.dll` and the folder `EvilDragon`
   next to `DiggingGame-Win64-Shipping.exe`.

### Step 3 – Play

Start the game, load your save and press **F1**. The menu opens on the left.
The menu loads about 10 seconds after the game starts.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | God mode (no fall or lava damage), infinite battery, heal & recharge, walk speed, jump height, gravity, unlimited jumps, fly mode through the ground |
| **Money** | Add / remove money, any ore one by one or all at once, empty the bag, unlimited bag space, **collect every ore in range** (once or automatically) |
| **Tools** | Drill strength up to 20x, dig radius up to 5x, jetpack power up to 5x, no depth limit, unlock drill & jetpack, dynamite / lamps / keys / teleporters |
| **Spawn** | Money bag, treasure key, treasure chest, mines, dynamite, explosions (adjustable size), lamps, mole – at the crosshair or in front of you, 1–25 at once |
| **Teleport** | Back to where you started, 3 saved positions |
| **Settings** | 6 themes, change keys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |

You can change every key in the **Settings** tab. While the menu is open your character stands still.

**Fly mode:** WASD to fly, Space up, Ctrl or C down, Shift faster.

**Save Settings:** in the *Settings* tab. Every cheat you turned on is on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Fix |
|:--|:--|
| F1 does nothing | Make sure `dxgi.dll` is right next to `DiggingGame-Win64-Shipping.exe` (in `DiggingGame\Binaries\Win64\`, **not** next to `DiggingGame.exe` at the top) with `EvilDragon\EvilDragon.DiggingAHoleCheats.dll` beside it. Wait about 10 seconds after starting. |
| I use ReShade | ReShade is often called `dxgi.dll` too. Rename ReShade to `d3d11.dll`. |
| My antivirus flags the dll | That is a false alarm, because the mod loads itself into the game. The full source code is in this repository. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `EvilDragon\EvilDragon.DiggingAHoleCheats.log`. |

## Uninstall

Delete the file `dxgi.dll` and the folder `EvilDragon` in `DiggingGame\Binaries\Win64\`.

## Notes

- Money, ores and items stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `EvilDragon\EvilDragon.DiggingAHoleCheats.ini`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Same as [Storage Hunter Simulator](../StorageHunter/README.en.md) – the shared parts live in [`shared/unreal`](../../shared/unreal).
Copy `src\local.bat.example` to `src\local.bat`, fill in your paths and run `src\build.bat`.
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
