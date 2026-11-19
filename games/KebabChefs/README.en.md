<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Kebab%20Chefs!%20Prologue&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-6%20(be.793)-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/platform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – an in-game cheat menu for <i>Kebab Chefs! - Restaurant Simulator Prologue</i>.</p>

> **Single-player only.** Kebab Chefs has online co-op. As soon as another player is in your session (or you play in someone else's), every cheat switches itself off.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Installing takes about 5 minutes and only has to be done once.

### Step 1 – Open the game folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Kebab Chefs! - Restaurant Simulator Prologue** → **Manage** → **Browse local files**.
3. A folder opens. You can see the file `Kebab Chefs! - Restaurant Simulator Prologue.exe`. **Keep this folder open.**

### Step 2 – Install BepInEx (the mod loader)

1. Open the BepInEx download page: <https://builds.bepinex.dev/projects/bepinex_be>
2. The newest build is at the top (number **793 or higher**). Download
   **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.XXX....zip`**
   (it must say **Unity.IL2CPP** and **win-x64** – the other files do not work).
3. Open the zip and copy **everything inside** into your game folder from step 1.
4. Check: next to the `.exe` you now have the file `winhttp.dll` and the folder `BepInEx`.

### Step 3 – Start the game once

1. Start the game normally through Steam.
2. A black console window opens next to the game. **That is normal.**
3. The first start takes **2–5 minutes** while BepInEx sets itself up. Wait for the main menu.
4. Close the game.

### Step 4 – Install the cheats

1. Go to the [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) page and download
   **`EvilDragons-Cheats-KebabChefs-v1.0.0.zip`** (always take the newest version).
2. Open the zip and copy the `BepInEx` folder inside it into your game folder.
   If Windows asks to merge/replace, click **Yes**.
3. Check: the file `EvilDragon.KebabChefsCheats.dll` is now in `BepInEx\plugins\`.

### Step 5 – Play

Start the game, load your restaurant and press **F1**. The menu opens on the left.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | What you can do |
|:--|:--|
| **Player** | Walk speed, jump height, freeze time, day speed, ingredients never rot, **instant interact** (no more holding, progress bars 20x faster), **blackjack helper** (shows HIT or STAY), **lucky cards** (you get the best card, the dealer the worst) |
| **Money** | Add / remove / set money, income multiplier up to x100, festival & event coins, pay off every bill & loan |
| **Restaurant** | XP & level, cook points, any perk one by one or all of them to max for free |
| **Items** | Spawn any ingredient or item (1–50 at once) – with search |
| **Settings** | 6 themes, change keys, Save Settings |

## Hotkeys

| Key | Action |
|:--:|:--|
| <kbd>F1</kbd> | Open / close the menu |
| <kbd>F2</kbd> | Add the selected amount of money |

You can change both keys in the **Settings** tab. While the menu is open your character stands still and the camera does not turn.

**Searching items:** click the search field and type. Click anywhere else when you are done. **Ctrl+A** selects the search text – Backspace clears it, typing replaces it.

**Save Settings:** in the *Settings* tab. Every cheat you turned on is on again the next time you play.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Problems?

| Problem | Fix |
|:--|:--|
| No black console window | BepInEx is not installed correctly. `winhttp.dll` must be right next to the `.exe`, not in a sub folder. Make sure you downloaded the **Unity.IL2CPP win-x64** version. |
| F1 does nothing | Check that `EvilDragon.KebabChefsCheats.dll` is in `BepInEx\plugins\` and that you started the game once before (step 3). |
| The menu says "cheats are disabled" | Other players are in the session. Play alone. |
| Menu gone after a game update | Delete the `BepInEx\interop` folder and restart the game – BepInEx rebuilds it. |
| Something else | Open an [issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) and attach `BepInEx\LogOutput.log`. |

## Uninstall

- **Only the cheats:** delete `BepInEx\plugins\EvilDragon.KebabChefsCheats.dll`.
- **Everything:** also delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folders `BepInEx` and `dotnet` from the game folder.

## Notes

- Money, XP, cook points, perks and spawned items stay in your save.
- Steam achievements can still unlock while cheats are on.
- Your settings are stored in `BepInEx\config\evildragon.cheats.kebabchefs.cfg`.

<details>
<summary><b>Build from source</b> (for developers)</summary>

Requirements: .NET SDK 6.0 or newer and the game with BepInEx installed and started once.

```powershell
cd games/KebabChefs/src
copy GameDir.props.example GameDir.props   # put your game folder in here
dotnet build -c Release
```

The DLL is copied to `BepInEx\plugins` automatically (unless the game is running).
This game strips GUILayout completely, so the menu uses the own layout from `shared/EvilDragonUi.cs` (`Ui.Manual`).
</details>

## Changelog

**1.0.0**
- First version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
