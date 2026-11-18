<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Can%20%27Em%20All&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-2.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6%20IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-6%20(be.793)-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>Can 'Em All</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 5 Minuten und muss nur einmal gemacht werden.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **Can 'Em All** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Datei `Can 'Em All.exe`. **Lass diesen Ordner offen.**

### Schritt 2 – BepInEx installieren (der Mod-Loader)

1. Öffne die BepInEx-Downloadseite: <https://builds.bepinex.dev/projects/bepinex_be>
2. Ganz oben steht der neueste Build (Nummer **793 oder höher**). Lade die Datei
   **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.XXX....zip`** herunter
   (es muss **Unity.IL2CPP** und **win-x64** drinstehen – die anderen Dateien funktionieren nicht).
3. Öffne die ZIP und kopiere **alles darin** in deinen Spielordner aus Schritt 1.
4. Kontrolle: Neben `Can 'Em All.exe` liegen jetzt die Datei `winhttp.dll` und der Ordner `BepInEx`.

### Schritt 3 – Spiel einmal starten

1. Starte das Spiel ganz normal über Steam.
2. Neben dem Spiel öffnet sich ein schwarzes Konsolenfenster. **Das ist normal.**
3. Der erste Start dauert **1–3 Minuten**, weil BepInEx sich einrichtet. Warte, bis das Hauptmenü erscheint.
4. Schließ das Spiel.

### Schritt 4 – Cheats installieren

1. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-CanEmAll-v2.0.0.zip`** herunter.
2. Öffne die ZIP und kopiere den Ordner `BepInEx` darin in deinen Spielordner.
   Wenn Windows fragt, ob zusammengeführt/ersetzt werden soll, klick auf **Ja**.
3. Kontrolle: Die Datei `EvilDragon.CanEmAllCheats.dll` liegt jetzt in `BepInEx\plugins\`.

### Schritt 5 – Spielen

Starte das Spiel, lade deinen Spielstand und drück **F1**. Das Menü öffnet sich links.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | Godmode, One-Hit-Kill, unendlich Munition (HUD zeigt **∞**), Auto-Kill, unendlich Ausdauer, Laufspeed bis 10x, Spieltempo 0,25x–5x, Tages-Timer einfrieren, alle Gegner töten |
| **Money** | Geld hinzufügen / abziehen / setzen (Vorauswahl oder eigener Betrag), Einnahmen-Multiplikator bis x100, Rebirth-Punkte |
| **Factory** | Maschinen 20x schneller, Fließband x4, alle Dosen shiny, Extra-Dosen pro Gegner, Turrets mit unendlich Munition, alle Maschinen einschalten, alle Skills freischalten |
| **Spawn** | Jede Kreatur spawnen (1–1000, optional shiny) am Fadenkreuz oder vor dir, Dosen mit beliebigem Wert spawnen, Kreaturen / Dosen / alles zum Fadenkreuz oder zu dir teleportieren |
| **Settings** | 6 Themes, Tasten ändern, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>K</kbd> | Alle Gegner töten |
| <kbd>T</kbd> | Alle Kreaturen und Dosen teleportieren |
| <kbd>J</kbd> | Ausgewählte Kreatur spawnen |

Alle Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, ignoriert das Spiel deine Maus – du schießt also nicht, wenn du einen Button anklickst.

**Eigene Zahl eingeben:** Klick auf das dunkle Feld (z.B. *Custom count*), tipp die Zahl ein und drück **Enter**.

**Save Settings:** im Tab *Settings* (oder *Player*). Alle eingeschalteten Cheats sind beim nächsten Spielstart wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| Kein schwarzes Konsolenfenster | BepInEx ist nicht richtig installiert. `winhttp.dll` muss direkt neben `Can 'Em All.exe` liegen, nicht in einem Unterordner. Achte darauf, dass du die Version **Unity.IL2CPP win-x64** geladen hast. |
| F1 tut nichts | Prüfe, ob `EvilDragon.CanEmAllCheats.dll` in `BepInEx\plugins\` liegt und ob du das Spiel vorher einmal gestartet hast (Schritt 3). |
| Menü weg nach einem Spiel-Update | Lösch den Ordner `BepInEx\interop` und starte das Spiel neu – BepInEx baut ihn neu auf. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `BepInEx\LogOutput.log` an. |

## Deinstallieren

- **Nur die Cheats:** lösch `BepInEx\plugins\EvilDragon.CanEmAllCheats.dll`.
- **Alles:** lösch zusätzlich `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` und die Ordner `BepInEx` und `dotnet` aus dem Spielordner.

## Hinweise

- Nur für Einzelspieler.
- Steam-Erfolge können auch mit aktiven Cheats freigeschaltet werden.
- Deine Einstellungen liegen in `BepInEx\config\evildragon.cheats.canemall.cfg`.

<details>
<summary><b>Aus dem Quellcode bauen</b> (für Entwickler)</summary>

Voraussetzungen: .NET SDK 6.0 oder neuer und das Spiel mit installiertem und einmal gestartetem BepInEx.

```powershell
cd games/CanEmAll/src
copy GameDir.props.example GameDir.props   # trag hier deinen Spielordner ein
dotnet build -c Release
```

Die DLL wird automatisch nach `BepInEx\plugins` kopiert (außer das Spiel läuft gerade).
</details>

## Changelog

**2.0.0**
- Neuer Name: EvilDragon's Cheats, mit Logo
- 6 Themes (Dragon, Neon, Inferno, Ocean, Purple, Gold)
- Save Settings – deine Cheats sind beim nächsten Start wieder an
- Behoben: Der Raketenwerfer hat mit unendlich Munition endlos nachgeladen
- Englisches Menü, größere und fettere Schrift, Hotkey-Leiste

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
