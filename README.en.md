<img src="https://capsule-render.vercel.app/api?type=waving&color=0:00ff4c,50:00b341,100:04140a&height=200&section=header&text=EvilDragon%27s%20Cheats&fontColor=ffffff&fontSize=52&fontAlignY=36&desc=Clean%20in-game%20cheat%20menus%20for%20single-player%20games&descAlignY=58&descSize=16" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="assets/logo.png" width="160" alt="EvilDragon logo" />
</p>

<p align="center">
  <a href="https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases"><img src="https://img.shields.io/github/v/release/AdminEvilDRagon/EvilDragons-Cheats?style=for-the-badge&color=00ff4c&labelColor=0b1a10&logo=github" /></a>
  <a href="https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases"><img src="https://img.shields.io/github/downloads/AdminEvilDRagon/EvilDragons-Cheats/total?style=for-the-badge&color=00e5ff&labelColor=0b1a10" /></a>
  <img src="https://img.shields.io/badge/games-6-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/single--player-only-ffb300?style=for-the-badge&labelColor=0b1a10" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-a36bff?style=for-the-badge&labelColor=0b1a10" /></a>
</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Games

| Game | Version | Download | Guide |
|:--|:--:|:--|:--|
| **Can 'Em All** | `2.0.0` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/CanEmAll/README.en.md) |
| **Crime Simulator** | `1.2.1` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/CrimeSimulator/README.en.md) |
| **Pumping Simulator 2** | `1.0.1` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/PumpingSimulator2/README.en.md) |
| **Storage Hunter Simulator** | `1.0.0` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/StorageHunter/README.en.md) |
| **A Game About Digging A Hole** | `1.0.0` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/DiggingAHole/README.en.md) |
| **Kebab Chefs! Prologue** | `1.0.0` | [Releases](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases) | [Install guide & features](games/KebabChefs/README.en.md) |

More games will be added over time. Each game has its own folder under [`games/`](games) with its own install guide.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## How it works

The cheats are a small mod file (a `.dll`) that is loaded into the game by a free mod loader called **BepInEx**.
You install BepInEx once, put the mod file into it, start the game and press **F1** to open the menu.

Unreal games like Storage Hunter Simulator don't need BepInEx – the loader is right in the zip.

The exact steps are different for every game, so always follow the guide of your game in the table above.

## Every menu has

- **Themes** – Dragon, Neon, Inferno, Ocean, Purple, Gold
- **Hotkeys** – use the most important cheats without opening the menu (can be changed)
- **Save Settings** – your cheats are switched on again the next time you play
- **No changes until you want them** – nothing happens until you turn a cheat on, your game files stay untouched

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Repository layout

```
EvilDragons-Cheats/
├── assets/                 logo & images
├── shared/                 menu design used by every game (themes, logo, buttons)
│   └── unreal/             loader, overlay & menu for the Unreal games
└── games/
    ├── CanEmAll/
    │   ├── README.md       install guide (German)
    │   ├── README.en.md    install guide (English)
    │   └── src/            source code (C#)
    ├── CrimeSimulator/
    ├── KebabChefs/
    ├── PumpingSimulator2/
    ├── StorageHunter/      (Unreal Engine, C++)
    └── DiggingAHole/       (Unreal Engine, C++)
```

## Disclaimer

These mods are made **for single-player/offline only**. Games with online modes switch every cheat off automatically while other players are in your session, and scores are never uploaded to leaderboards.
This project is not affiliated with or endorsed by any game developer or publisher. All game names and trademarks belong to their owners.
No game files are included in this repository.

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=110&section=footer" width="100%" />
