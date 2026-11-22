<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Potato%20Please&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6000.3%20IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-6%20(be.793)-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Potato Please</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

The installation takes about 5 minutes and only has to be done once.

### Step 1 – Open your game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Potato Please** → **Manage** → **Browse local files**.
3. A folder opens. You should see the file `PotatoPlease.exe` in it. **Keep this folder open.**

### Step 2 – Install BepInEx (the mod loader)

1. Open the BepInEx download page: <https://builds.bepinex.dev/projects/bepinex_be>
2. At the top you see the newest build (number **793 or higher**). Download the file named
   **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.XXX....zip`**
   (it must say **Unity.IL2CPP** and **win-x64** – the other files will not work).
3. Open the downloaded ZIP and copy **everything inside it** into your game folder from step 1.
4. Check: next to `PotatoPlease.exe` there should now be a file `winhttp.dll` and a folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam.
2. A black console window opens next to the game. **This is normal.**
3. The first start takes **1–3 minutes** while BepInEx prepares itself. Wait until the main menu appears.
4. Close the game.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-PotatoPlease-v1.0.0.zip`**.
2. Open the ZIP and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks whether to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.PotatoPleaseCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your save and press **F1**. The menu opens on the left side.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Walk speed up to 5x, jump height, **fly mode** (through walls and over the snow), game speed |
| **Money** | Add / remove rubles (presets or your own amount), **add skill points** |
| **Upgrades** | Buy every upgrade and skill one by one or **max everything at once** – for free |
| **Survival** | **Infinite stamina**, always warm (you can not freeze), no cough, **the knife never gets dull** |
| **Spawn** | **Spawn potatoes in front of you**, refill the big potato pile, new knife, new letters, poison vial, **vegetable crates** |
| **Teleport** | Save a position, workstation, upgrade terminal, delivery zone, machines, mini games, **hidden items** |
| **Settings** | 6 themes, **menu size**, change keys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Fly mode on / off |
| <kbd>F3</kbd> | Add the selected amount of money |

You can change all keys in the **Settings** tab. F1 no longer opens the game's pause menu – you still pause with **Esc**. If a tab is taller than the window, scroll with the mouse wheel.

**Fly mode:** WASD, Space goes up, **Ctrl** goes down, **Shift** is faster.

**Typing your own number:** click the dark field (e.g. *Custom amount*), type the number and press **Enter**.

**Save Settings:** in the *Settings* tab. All cheats you have switched on and your settings are back the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Solution |
|:--|:--|
| No black console window appears | BepInEx is not installed correctly. `winhttp.dll` must be directly next to `PotatoPlease.exe`, not in a subfolder. Make sure you downloaded the **Unity.IL2CPP win-x64** version. |
| The menu says "Cheats are off" | Load your save first – the cheats are off in the main menu. |
| F1 does nothing | Check that `EvilDragon.PotatoPleaseCheats.dll` is in `BepInEx\plugins\` and that you started the game once before (step 3). |
| The menu is gone after a game update | Delete the folder `BepInEx\interop` and start the game again – BepInEx rebuilds it. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.PotatoPleaseCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folders `BepInEx` and `dotnet` from the game folder.

## Notes

- Single-player only.
- Money, skill points and upgrades stay in your save once the game saves.
- **Leaderboards:** with the mod the game uploads nothing to the Steam leaderboards and sends no statistics.
- **New Knife** and **New Letters** only work once the knife sharpener or the mailbox is there in your game.
- If the menu looks too big or too small, change the **Menu Size** in the *Settings* tab.
- Your settings are stored in `BepInEx\config\evildragon.cheats.potatoplease.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer, and the game with BepInEx installed and started once.

```powershell
cd games/PotatoPlease/src
copy GameDir.props.example GameDir.props   # put your game folder path into this file
dotnet build -c Release
```

The DLL is copied into `BepInEx\plugins` automatically (unless the game is running).
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
