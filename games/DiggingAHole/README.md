<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=A%20Game%20About%20Digging%20A%20Hole&fontColor=ffffff&fontSize=36&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unreal-5.5-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-nicht%20n%C3%B6tig-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>A Game About Digging A Hole</i>.</p>

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 2 Minuten. Du brauchst **keinen** Mod-Loader – alles ist in der ZIP.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **A Game About Digging A Hole** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Ordner `Engine` und `DiggingGame`. **Lass diesen Ordner offen.**

### Schritt 2 – Cheats installieren

1. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-DiggingAHole-v1.0.0.zip`** herunter (nimm immer die neueste Version).
2. Öffne die ZIP und kopiere den Ordner `DiggingGame` darin in deinen Spielordner.
   Wenn Windows fragt, ob zusammengeführt/ersetzt werden soll, klick auf **Ja**.
3. Kontrolle: In `DiggingGame\Binaries\Win64\` liegen jetzt neben `DiggingGame-Win64-Shipping.exe`
   die Datei `dxgi.dll` und der Ordner `EvilDragon`.

### Schritt 3 – Spielen

Starte das Spiel, lade deinen Spielstand und drück **F1**. Das Menü öffnet sich links.
Das Menü wird etwa 10 Sekunden nach dem Start geladen.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | Godmode (kein Fall- & Lavaschaden), unendlich Akku, heilen & aufladen, Lauftempo, Sprunghöhe, Schwerkraft, unendlich Sprünge, Flugmodus durch den Boden |
| **Money** | Geld hinzufügen / abziehen, jedes Erz einzeln oder alle auf einmal, Tasche leeren, unbegrenzter Taschenplatz, **alle Erze in Reichweite einsammeln** (einmal oder automatisch) |
| **Tools** | Bohrkraft bis 20x, Grabradius bis 5x, Jetpack-Kraft bis 5x, keine Tiefengrenze, Bohrer & Jetpack freischalten, Dynamit / Lampen / Schlüssel / Teleporter |
| **Spawn** | Geldsack, Schatzschlüssel, Schatztruhe, Minen, Dynamit, Explosionen (Größe einstellbar), Lampen, Maulwurf – am Fadenkreuz oder vor dir, 1–25 Stück |
| **Teleport** | Zurück zum Startpunkt, 3 Speicherplätze für Positionen |
| **Settings** | 6 Themes, Tasten ändern, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>F2</kbd> | Ausgewählten Geldbetrag hinzufügen |
| <kbd>F3</kbd> | Flugmodus an / aus |

Alle Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, bleibt deine Figur stehen.

**Flugmodus:** WASD zum Fliegen, Leertaste hoch, Strg oder C runter, Shift schneller.

**Save Settings:** im Tab *Settings*. Alle eingeschalteten Cheats sind beim nächsten Spielen wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| F1 tut nichts | Prüfe, ob `dxgi.dll` direkt neben `DiggingGame-Win64-Shipping.exe` liegt (in `DiggingGame\Binaries\Win64\`, **nicht** neben der `DiggingGame.exe` ganz oben) und `EvilDragon\EvilDragon.DiggingAHoleCheats.dll` daneben. Warte nach dem Start etwa 10 Sekunden. |
| Ich nutze ReShade | ReShade heißt oft auch `dxgi.dll`. Benenne ReShade in `d3d11.dll` um. |
| Virenscanner meldet die DLL | Das ist ein Fehlalarm, weil sich die Mod ins Spiel lädt. Der komplette Quellcode liegt in diesem Repository. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `EvilDragon\EvilDragon.DiggingAHoleCheats.log` an. |

## Deinstallieren

Lösch in `DiggingGame\Binaries\Win64\` die Datei `dxgi.dll` und den Ordner `EvilDragon`.

## Hinweise

- Geld, Erze und Items bleiben in deinem Spielstand.
- Steam-Erfolge können auch mit aktiven Cheats freigeschaltet werden.
- Deine Einstellungen liegen in `EvilDragon\EvilDragon.DiggingAHoleCheats.ini`.

<details>
<summary><b>Aus dem Quellcode bauen</b> (für Entwickler)</summary>

Wie bei [Storage Hunter Simulator](../StorageHunter) – die gemeinsamen Teile liegen in [`shared/unreal`](../../shared/unreal).
`src\local.bat.example` nach `src\local.bat` kopieren, Pfade eintragen und `src\build.bat` ausführen.
</details>

## Changelog

**1.0.0**
- Erste Version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
