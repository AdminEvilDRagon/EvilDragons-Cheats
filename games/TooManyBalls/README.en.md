<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Too%20Many%20Balls&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-2022.3%20Mono-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Too Many Balls</i>.</p>


<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Too Many Balls** → **Manage** → **Browse local files**.
3. A folder opens. You can see the file `Too Many Balls.exe`. **Keep this folder open.**

### Step 2 – Install BepInEx 5 (the mod loader)

1. Open the BepInEx releases page: <https://github.com/BepInEx/BepInEx/releases>
2. Find the newest **5.4.x** version (not 6.x!) and download **`BepInEx_win_x64_5.4.xx.zip`**.
3. Open the ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `Too Many Balls.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam and wait until the main menu appears.
2. Close the game. BepInEx has now created the folder `BepInEx\plugins`.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-TooManyBalls-v1.0.0.zip`** (always take the newest version).
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.TooManyBallsCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Walk speed up to 5x, jump up to x3, unlock sprint and jump, **fly mode** (through walls and over every fence), game speed |
| **Money** | Add / remove money, **free upgrades** (helpers work without wages too), ball price x2 to x100, **sell all balls from anywhere** |
| **Balls** | **Bottomless basket**, pick up without waiting, **bigger grab** (up to 200 balls per click), **collect every ball in the area**, mark the last balls |
| **Tools** | **Unlock every tool**, infinite bombs, bombs pull every ball, bomb range, vacuum never overheats, vacuum range, scanner and teleport without cooldown |
| **Upgrades** | Set every upgrade with − / + / Max (the 3 helpers too), **max everything at once** |
| **Areas** | Every area with the balls left – teleport there or collect all of them from anywhere |
| **Teleport** | Save a position, sell basket, basket upgrades, owner, mini games, people, **cassettes and lost items** (with markers) |
| **Settings** | 6 themes, **menu size**, change keys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Fly mode on / off |
| <kbd>F3</kbd> | Collect every ball in the area you are in |
| <kbd>F4</kbd> | Sell all your balls (wherever you are) |

You can change all keys in the **Settings** tab. While the menu is open your character and the camera stand still. If a tab is taller than the window, scroll with the mouse wheel.

**Fly mode:** WASD, Space goes up, **Ctrl** goes down, **Shift** is faster.

**Typing your own number:** click the dark field (e.g. *Custom amount*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab. Every cheat you turned on and your settings are back the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No mod loads at all | `winhttp.dll` must be directly next to `Too Many Balls.exe`, not in a subfolder. Make sure you downloaded **BepInEx 5** for **win x64**. |
| The menu says "Cheats are off" | Load your save first – the cheats are off in the main menu. |
| F1 does nothing | Check that `EvilDragon.TooManyBallsCheats.dll` is in `BepInEx\plugins\`. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.TooManyBallsCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folder `BepInEx` from the game folder.

## Notes

- Money, upgrades, unlocks and collected balls stay in your save once the game saves.
- **Collect** only takes as many balls as fit in your basket – with **Bottomless Basket** it takes all of them.
- If the menu looks too big or too small, change **Menu Size** in the *Settings* tab.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.toomanyballs.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx 5 installed.

```powershell
cd games/TooManyBalls/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
