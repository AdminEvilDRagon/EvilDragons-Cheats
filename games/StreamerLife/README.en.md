<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Streamer%20Life%20Simulator&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-2019.3%20Mono-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Streamer Life Simulator</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Streamer Life Simulator** → **Manage** → **Browse local files**.
3. A folder opens. Open the **`windows_content`** folder inside it – `Streamer Life Simulator.exe` is in there. **That is your game folder, keep it open.**

### Step 2 – Install BepInEx 5 (the mod loader)

1. Open the BepInEx releases page: <https://github.com/BepInEx/BepInEx/releases>
2. Find the newest **5.4.x** version (not 6.x!) and download **`BepInEx_win_x64_5.4.xx.zip`**.
3. Open the ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `Streamer Life Simulator.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam and wait until the main menu appears.
2. Close the game. BepInEx has now created the folder `BepInEx\plugins`.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-StreamerLife-v1.0.0.zip`** (always take the newest version).
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.StreamerLifeCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | All needs always full (health, energy, hunger, hygiene, toilet, happiness – never tired), walk speed up to 5x, jump up to x3, fly mode through walls |
| **Money** | Money on your bank card or in your wallet (presets or your own amount), remove money, Bitcoin |
| **Stream** | Followers, subs, live viewers (while you stream), streamer points for the skill tree |
| **Items** | Spawn any item – in front of you or straight into your inventory, 1–10 at once, with categories and search |
| **Best** | **Best Of Everything** (the best item of every kind), **Best PC** (case, CPU + matching mainboard, 4x RAM, 2x graphics card, 2x SSD), the best item of one kind, **best house** or **all houses** for free |
| **Teleport** | Bring your cars to you, jump to all 5 houses, the gas station, pet shop, people and places, save a position |
| **World** | Freeze time, day speed, set the time to morning / noon / evening / night |
| **Settings** | 6 themes, change keys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |
| <kbd>F3</kbd> | Fly mode on / off |

You can change all keys in the **Settings** tab. While the menu is open your character stands still and the camera does not turn. If a tab is taller than the window, scroll with the mouse wheel.

**Fly mode:** WASD to fly, Space up, Ctrl or C down, Shift faster.

**Searching items:** click the search field and type. Click anywhere else when you are done. **Ctrl+A** selects the search text – Backspace clears it, typing replaces it.

**Typing your own number:** click the dark field (e.g. *Custom amount*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab. Every cheat you turned on is on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No mod loads at all | `winhttp.dll` must be directly next to `Streamer Life Simulator.exe`, not in a subfolder. Make sure you downloaded **BepInEx 5** for **win x64**. |
| F1 does nothing | Check that `EvilDragon.StreamerLifeCheats.dll` is in `BepInEx\plugins\`. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.StreamerLifeCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folder `BepInEx` from the game folder.

## Notes

- Money, followers, subs, points, houses and spawned items stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.streamerlife.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx 5 installed.

```powershell
cd games/StreamerLife/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
