<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Pumping%20Simulator%202&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-2022.3%20Mono-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Pumping Simulator 2</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Pumping Simulator 2** → **Manage** → **Browse local files**.
3. A folder opens. You should see the file `Pumping Simulator 2.exe` in it. **Keep this folder open.**

### Step 2 – Install BepInEx 5 (the mod loader)

1. Open the BepInEx releases page: <https://github.com/BepInEx/BepInEx/releases>
2. Find the newest **5.4.x** version (not 6.x!) and download **`BepInEx_win_x64_5.4.xx.zip`**.
3. Open the ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `Pumping Simulator 2.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam and wait until the main menu appears.
2. Close the game. BepInEx has now created the folder `BepInEx\plugins`.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-PumpingSimulator2-v1.0.0.zip`**.
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.PumpingSimulator2Cheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Infinite stamina, no hunger & thirst, walk speed up to 5x, jump power up to x3, fly mode with adjustable speed |
| **Money** | Add or remove money (presets or your own amount), add reputation XP |
| **Station** | Infinite fuel, fill the tank, workers never get tired, fast delivery, max all upgrades |
| **World** | Freeze time, set the time to morning / noon / evening / night |
| **Items** | Spawn anything from the order catalog as a delivery box (1–100 pieces) at your crosshair or in front of you – with categories and search |
| **Settings** | 6 themes, change hotkeys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected money amount |
| <kbd>F3</kbd> | Fill the station's fuel tank |
| <kbd>F4</kbd> | Fly mode on / off |

You can change every hotkey in the **Settings** tab. While the menu is open, your character stands still, so you don't walk around while clicking.

**Searching items:** click the search field and type. Click anywhere else when you are done.

**Typing your own number:** click the dark field (e.g. *Custom amount*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab. All cheats you have switched on are switched on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No mod loads at all | `winhttp.dll` must be directly next to `Pumping Simulator 2.exe`, not in a subfolder. Make sure you downloaded **BepInEx 5** for **win x64**. |
| F1 does nothing | Check that `EvilDragon.PumpingSimulator2Cheats.dll` is in `BepInEx\plugins\`. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.PumpingSimulator2Cheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folder `BepInEx` from the game folder.

## Notes

- Money, reputation, upgrades and spawned items stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.pumpingsimulator2.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx 5 installed.

```powershell
cd games/PumpingSimulator2/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**1.0.0**
- First release

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
