<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Find%20The%20Needle&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Godot-4.7-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/mod--loader-nicht%20n%C3%B6tig-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>Find The Needle</i>.</p>

> **Inoffizielle Mod.** Nicht vom Entwickler von Find The Needle, Nutzung auf eigene Gefahr. Du brauchst das **volle Spiel** – die Mod enthält keine Dateien des Spiels.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 2 Minuten. Du brauchst **keinen** Mod-Loader – alles ist in der ZIP.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **Find The Needle** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Datei `FindTheNeedle.exe`. **Lass diesen Ordner offen.**

### Schritt 2 – Cheats installieren

1. **Schließ das Spiel.**
2. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-FindTheNeedle-v1.0.0.zip`** herunter (nimm immer die neueste Version).
3. Öffne die ZIP und kopiere **alle drei Dateien** (`EvilDragon.FindTheNeedleCheats.dll`, `install.bat`, `uninstall.bat`) in deinen Spielordner.
4. Doppelklick auf **`install.bat`**. Es meldet „Done!“.

`install.bat` benennt die Terrain-Erweiterung des Spiels in `libterrain.windows.release.x86_64.original.dll` um und setzt die Mod an ihre Stelle. Die Mod lädt das Original selbst – das Gelände sieht genauso aus wie vorher.

### Schritt 3 – Spielen

Starte das Spiel, lade einen Spielstand und drück **F1**. Das Menü öffnet sich links.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | **Unendlich Ausdauer**, **unendlich Jetpack**, **kein Falltod**, Ausdauer & Jetpack auffüllen, Laufspeed bis 5x, **Flugmodus / Noclip** (der eigene des Spiels – durch Heu, Wände und Boden) |
| **Money** | Geld hinzufügen / abziehen (Vorauswahl oder eigener Betrag), **Schulden abbezahlen** |
| **Upgrades** | **Alle 8 Werkzeuge** (einzeln oder alle), der **komplette Tech-Baum** nach Bereichen – jedes Upgrade einzeln oder **alles auf einmal freischalten** (gratis) |
| **Spawn** | **Jedes Item** aus dem Spiel spawnen (1x, 5x oder 10x, mit Suche) – es landet vor dir. Tragbares wie der Jetpack wird direkt freigeschaltet |
| **Teleport** | Zu **jeder Nadel**, die noch im Heu steckt (mit Name und Entfernung), zum Schlüssel, zum Yard-Start, **3 Positionen speichern** |
| **Settings** | 6 Themes, Tasten ändern, Benachrichtigungston an / aus, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>F2</kbd> | Ausgewählten Geldbetrag hinzufügen |
| <kbd>F3</kbd> | Flugmodus an / aus |

Alle Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, hast du die Maus frei.

**Benachrichtigungen:** Jede Aktion meldet sich unten links mit einer kurzen Glas-Karte und einem leisen Klick. Den Ton kannst du im Tab *Settings* ausschalten.

**Save Settings:** im Tab *Settings*. Die eingeschalteten Cheats sind beim nächsten Spielen wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| F1 tut nichts | Lauf `install.bat` noch einmal (bei geschlossenem Spiel). Nach einem **Spiel-Update** musst du das immer machen – Steam ersetzt dabei die Terrain-Datei. |
| `install.bat` sagt, die Datei fehlt | Rechtsklick auf das Spiel in Steam → **Eigenschaften → Installierte Dateien → Dateien auf Fehler überprüfen**, dann `install.bat` erneut. |
| Virenscanner meldet die DLL | Das ist ein Fehlalarm, weil sich die Mod ins Spiel lädt. Der komplette Quellcode liegt in diesem Repository. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `EvilDragon.FindTheNeedleCheats.log` aus dem Spielordner an. |

## Deinstallieren

Doppelklick auf **`uninstall.bat`** (bei geschlossenem Spiel) – die Terrain-Datei des Spiels kommt zurück. Danach kannst du `EvilDragon.FindTheNeedleCheats.dll`, `install.bat` und `uninstall.bat` löschen.

## Hinweise

- **Bestenlisten:** Sobald du einen Cheat benutzt, markiert die Mod den Lauf über die eigenen Flags des Spiels als Debug/Modded – Cheat-Läufe landen nicht in den Online-Bestenlisten.
- Geld, Upgrades, Werkzeuge und gespawnte Items bleiben in deinem Spielstand.
- Deine Einstellungen liegen in `EvilDragon.FindTheNeedleCheats.ini` im Spielordner.

<details>
<summary><b>Wie die Mod funktioniert / aus dem Quellcode bauen</b> (für Entwickler)</summary>

Find The Needle ist ein Godot-4.7-Spiel mit verschlüsselten Spieldaten. Die Mod entschlüsselt nichts: Sie gibt sich als die
Terrain3D-Erweiterung (`libterrain…dll`) aus, reicht deren Start an die echte DLL weiter und bekommt so die offizielle
GDExtension-Schnittstelle von Godot. Darüber liest und ändert sie die Variablen der Spielskripte per Namen (`src/gd.h`) –
im Haupt-Thread des Spiels über `register_main_loop_callbacks`. Das Menü ist das ImGui-Overlay aus `shared/unreal` (D3D12).

`src/probe.cpp` ist ein Entwickler-Werkzeug: Es schreibt die Struktur des Spiels (Autoloads, Szene, Skript-Variablen) in Textdateien.

Voraussetzungen: Visual Studio mit C++-Tools, [Dear ImGui 1.91.5](https://github.com/ocornut/imgui), [MinHook](https://github.com/TsudaKageyu/minhook).

1. `src\local.bat.example` nach `src\local.bat` kopieren und die Pfade eintragen.
2. `src\build.bat` ausführen und `bin\libterrain.windows.release.x86_64.dll` als `EvilDragon.FindTheNeedleCheats.dll` neben `install.bat` legen.
</details>

## Changelog

**1.0.0**
- Erste Version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
