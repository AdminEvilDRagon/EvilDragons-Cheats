<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Crime%20Simulator&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.1.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-2022.3%20Mono-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Crime Simulator</i>.</p>

> **Single-player only.** Crime Simulator has online co-op. As soon as another player is in your session, every cheat switches itself off automatically. Scores are never uploaded to Steam leaderboards while the mod is installed.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Crime Simulator** → **Manage** → **Browse local files**.
3. A folder opens. You should see the file `Crime Simulator.exe` in it. **Keep this folder open.**

### Step 2 – Install BepInEx 5 (the mod loader)

1. Open the BepInEx releases page: <https://github.com/BepInEx/BepInEx/releases>
2. Find the newest **5.4.x** version (not 6.x!) and download **`BepInEx_win_x64_5.4.xx.zip`**.
3. Open the ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `Crime Simulator.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam and wait until the main menu appears.
2. Close the game. BepInEx has now created the folder `BepInEx\plugins`.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-CrimeSimulator-v1.1.0.zip`** (always take the newest version).
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.CrimeSimulatorCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load a **single-player** save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | God mode, infinite stamina, full heal, walk speed up to 5x |
| **Stealth** | No police, call off the cops, undetectable, silent (no noise), no heat |
| **Gear** | No durability loss, infinite ammo, repair all items, damage up to one-hit, range up to x5, no attack cooldown, faster attacks, no attack stamina, instant lockpick, unbreakable lockpicks |
| **Money** | Add or remove money (presets or your own amount), add XP, add skill points |
| **Items** | Spawn any item (weapons, ammo, tools, skill books, loot, money …) at your crosshair or in front of you – with categories, search and amount. **Heist Plans:** one click spawns everything a robbery needs (e.g. the train station plan) |
| **Settings** | 6 themes, change hotkeys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Full heal |
| <kbd>F3</kbd> | Call off the cops |
| <kbd>F4</kbd> | Add the selected money amount |

You can change every hotkey in the **Settings** tab. While the menu is open, your character stands still (like in the pause menu), so you don't shoot or walk while clicking.

**Searching items:** click the search field and type. Click anywhere else when you are done.

**Typing your own number:** click the dark field (e.g. *Custom amount*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab. All cheats you have switched on are switched on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No mod loads at all | `winhttp.dll` must be directly next to `Crime Simulator.exe`, not in a subfolder. Make sure you downloaded **BepInEx 5** for **win x64**. |
| F1 does nothing | Check that `EvilDragon.CrimeSimulatorCheats.dll` is in `BepInEx\plugins\`. |
| Menu says "cheats are disabled" | You are in an online session with other players. Play single-player or alone. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.CrimeSimulatorCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folder `BepInEx` from the game folder.

## Notes

- Money, XP, skill points, ammo and spawned items you get with cheats stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.crimesimulator.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx 5 installed.

```powershell
cd games/CrimeSimulator/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**1.1.0**
- Heist Plans: spawn the items a robbery requires with one click
- New item categories *Skills* (skill books) and *Plans*
- More items found (e.g. all skill books)

**1.0.0**
- First release

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
