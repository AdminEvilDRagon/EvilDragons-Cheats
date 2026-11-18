<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Can%20%27Em%20All&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-2.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6%20IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-6%20(be.793)-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Can 'Em All</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Can 'Em All** → **Manage** → **Browse local files**.
3. A folder opens. You should see the file `Can 'Em All.exe` in it. **Keep this folder open.**

### Step 2 – Install BepInEx (the mod loader)

1. Open the BepInEx download page: <https://builds.bepinex.dev/projects/bepinex_be>
2. At the top you see the newest build (number **793 or higher**). Download the file named
   **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.XXX....zip`**
   (it must say **Unity.IL2CPP** and **win-x64** – the other files will not work).
3. Open the downloaded ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `Can 'Em All.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam.
2. A black console window opens next to the game. **This is normal.**
3. The first start takes **1–3 minutes** while BepInEx prepares itself. Wait until the main menu appears.
4. Close the game.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-CanEmAll-v2.0.0.zip`**.
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.CanEmAllCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | God mode, one-hit kill, infinite ammo (HUD shows ∞), auto kill, infinite stamina, walk speed up to 10x, game speed 0.25x–5x, freeze the day timer, kill all enemies |
| **Money** | Add, remove or set money (presets or your own amount), income multiplier up to x100, rebirth points |
| **Factory** | Machines 20x faster, conveyor x4, all cans shiny, extra cans per enemy, turrets with infinite ammo, turn on all machines, unlock all skills |
| **Spawn** | Spawn any creature (1–1000, shiny optional) at your crosshair or in front of you, spawn cans with any value, teleport creatures / cans / everything to your crosshair or to you |
| **Settings** | 6 themes, change hotkeys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>K</kbd> | Kill all enemies |
| <kbd>T</kbd> | Teleport all creatures and cans |
| <kbd>J</kbd> | Spawn the selected creature |

You can change every hotkey in the **Settings** tab. While the menu is open, the game ignores your mouse, so you don't shoot when you click a button.

**Typing your own number:** click the dark field (e.g. *Custom count*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab (or *Player* tab). All cheats you have switched on are switched on again the next time you start the game.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No black console window appears | BepInEx is not installed correctly. `winhttp.dll` must be directly next to `Can 'Em All.exe`, not in a subfolder. Make sure you downloaded the **Unity.IL2CPP win-x64** version. |
| F1 does nothing | Check that `EvilDragon.CanEmAllCheats.dll` is in `BepInEx\plugins\` and that you started the game once before (step 3). |
| The menu is gone after a game update | Delete the folder `BepInEx\interop` and start the game again – BepInEx rebuilds it. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.CanEmAllCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folders `BepInEx` and `dotnet` from the game folder.

## Notes

- Single-player only.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.canemall.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx installed and started once.

```powershell
cd games/CanEmAll/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**2.0.0**
- New name: EvilDragon's Cheats, with logo
- 6 themes (Dragon, Neon, Inferno, Ocean, Purple, Gold)
- Save Settings – your cheats are restored on the next start
- Fixed: rocket launcher kept reloading forever with infinite ammo
- English menu, bigger and bolder text, hotkey bar

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
