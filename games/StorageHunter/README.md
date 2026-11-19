<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Storage%20Hunter%20Simulator&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unreal-5.4-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-nicht%20n%C3%B6tig-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>Storage Hunter Simulator</i>.</p>

> **Nur Einzelspieler.** Storage Hunter Simulator hat Online-Koop. Sobald ein anderer Spieler in deiner Sitzung ist, schalten sich alle Cheats automatisch ab.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 2 Minuten. Du brauchst **keinen** Mod-Loader – alles ist in der ZIP.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **Storage Hunter Simulator** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Ordner `Engine` und `StorageHunter`. **Lass diesen Ordner offen.**

### Schritt 2 – Cheats installieren

1. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-StorageHunter-v1.0.0.zip`** herunter (nimm immer die neueste Version).
2. Öffne die ZIP und kopiere den Ordner `StorageHunter` darin in deinen Spielordner.
   Wenn Windows fragt, ob zusammengeführt/ersetzt werden soll, klick auf **Ja**.
3. Kontrolle: In `StorageHunter\Binaries\Win64\` liegen jetzt neben `StorageHunter-Win64-Shipping.exe`
   die Datei `dxgi.dll` und der Ordner `EvilDragon`.

### Schritt 3 – Spielen

Starte das Spiel, lade einen **Einzelspieler**-Spielstand und drück **F1**. Das Menü öffnet sich links.
Das Menü wird etwa 10 Sekunden nach dem Start geladen.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | Lauf- & Sprinttempo bis 5x, Sprunghöhe, Schwerkraft, unendlich Sprünge, Flugmodus (durch Wände) mit einstellbarem Tempo |
| **Money** | Geld hinzufügen / abziehen (Vorauswahl oder eigener Betrag), Renown & Level pro Stadtteil, Perk-Punkte, Tickets |
| **Items** | Jedes Item spawnen – mit Kategorien, Suche, Anzahl (1–20) und „schon verifiziert“. Kategorie **Special** für die besonderen Items |
| **Workshop** | Werkzeug-Level (Prüfen, Reparieren, Schlösser knacken), Prüfen & Reparieren klappt immer, Container entsperren, Zufalls-Items |
| **Business** | Pfandleihe leveln / kaufen, **jede Auktion gewinnen** (NPC-Bieter haben kein Geld), Aufgabe / Quest abschließen, unendlich Sprit, Tank füllen |
| **World** | Uhrzeit setzen (Morgen / Mittag / Abend / Nacht oder frei), Zeit einfrieren |
| **Settings** | 6 Themes, Tasten ändern, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>F2</kbd> | Ausgewählten Geldbetrag hinzufügen |
| <kbd>F3</kbd> | Flugmodus an / aus |

Alle Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, bleibt deine Figur stehen.

**Flugmodus:** WASD zum Fliegen, Leertaste hoch, Strg oder C runter, Shift schneller.

**Items spawnen:** Item anklicken und **Spawn** drücken – oder doppelt klicken. Die Items erscheinen vor dir.

**Save Settings:** im Tab *Settings*. Alle eingeschalteten Cheats sind beim nächsten Spielen wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| F1 tut nichts | Prüfe, ob `dxgi.dll` direkt neben `StorageHunter-Win64-Shipping.exe` liegt (in `StorageHunter\Binaries\Win64\`, **nicht** neben der `StorageHunter.exe` ganz oben) und `EvilDragon\EvilDragon.StorageHunterCheats.dll` daneben. Warte nach dem Start etwa 10 Sekunden. |
| Ich nutze ReShade | ReShade heißt oft auch `dxgi.dll`. Benenne ReShade in `d3d12.dll` um. |
| Virenscanner meldet die DLL | Das ist ein Fehlalarm, weil sich die Mod ins Spiel lädt. Der komplette Quellcode liegt in diesem Repository. |
| Menü sagt „Cheats are disabled“ | Du bist in einer Online-Sitzung mit anderen Spielern. Spiel allein. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `EvilDragon\EvilDragon.StorageHunterCheats.log` an. |

## Deinstallieren

Lösch in `StorageHunter\Binaries\Win64\` die Datei `dxgi.dll` und den Ordner `EvilDragon`.

## Hinweise

- Geld, Renown, Perk-Punkte und gespawnte Items bleiben in deinem Spielstand.
- Steam-Erfolge können auch mit aktiven Cheats freigeschaltet werden.
- Deine Einstellungen liegen in `EvilDragon\EvilDragon.StorageHunterCheats.ini`.

<details>
<summary><b>Aus dem Quellcode bauen</b> (für Entwickler)</summary>

Storage Hunter Simulator ist ein Unreal-Engine-Spiel, die Mod ist deshalb in C++ geschrieben (nicht C# wie die Unity-Mods).

Voraussetzungen: Visual Studio mit C++-Tools, Python 3, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook)
und ein SDK des Spiels, erzeugt mit [Dumper-7](https://github.com/Encryqed/Dumper-7).

1. `loader\build.bat` baut die `dxgi.dll` (lädt alle DLLs aus dem Ordner `EvilDragon`).
2. Dumper-7 einmal ins Spiel laden (DLL in den Ordner `EvilDragon` legen, Spiel starten) – das SDK landet in `C:\Dumper-7`.
3. `src\local.bat.example` nach `src\local.bat` kopieren und die Pfade eintragen.
4. `src\build.bat` ausführen. `tools\fix_sdk.py` repariert dabei automatisch einen Fehler im Dumper-7-SDK.
</details>

## Changelog

**1.0.0**
- Erste Version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
