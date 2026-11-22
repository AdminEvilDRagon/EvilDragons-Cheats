<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=One%20Million%20Coins&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-6000.6%20Mono-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>One Million Coins</i>.</p>


<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 5 Minuten und muss nur einmal gemacht werden.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **One Million Coins** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Datei `One Million Coins.exe`. **Lass diesen Ordner offen.**

### Schritt 2 – BepInEx 5 installieren (der Mod-Loader)

1. Öffne die BepInEx-Releases: <https://github.com/BepInEx/BepInEx/releases>
2. Such die neueste **5.4.x**-Version (nicht 6.x!) und lade **`BepInEx_win_x64_5.4.xx.zip`** herunter.
3. Öffne die ZIP und kopiere **alles darin** in deinen Spielordner aus Schritt 1.
4. Kontrolle: Neben `One Million Coins.exe` liegen jetzt die Datei `winhttp.dll` und der Ordner `BepInEx`.

### Schritt 3 – Spiel einmal starten

1. Starte das Spiel ganz normal über Steam und warte, bis das Hauptmenü erscheint.
2. Schließ das Spiel. BepInEx hat jetzt den Ordner `BepInEx\plugins` angelegt.

### Schritt 4 – Cheats installieren

1. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-OneMillionCoins-v1.0.0.zip`** herunter (nimm immer die neueste Version).
2. Öffne die ZIP und kopiere den Ordner `BepInEx` darin in deinen Spielordner.
   Wenn Windows fragt, ob zusammengeführt/ersetzt werden soll, klick auf **Ja**.
3. Kontrolle: Die Datei `EvilDragon.OneMillionCoinsCheats.dll` liegt jetzt in `BepInEx\plugins\`.

### Schritt 5 – Spielen

Starte das Spiel, starte oder lade deinen Durchlauf und drück **F1**. Das Menü öffnet sich links.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | Laufspeed bis 5x, Sprunghöhe bis x3, **Flugmodus** (durch Wände und durchs Gold), Spieltempo |
| **Coins** | **Münzen direkt einzahlen** oder in den Beutel packen (die Münzen kommen echt aus dem Goldhaufen), **Beutel ohne Limit**, **Beutel von überall einzahlen**, automatisch einzahlen, **Münz-Magnet** mit 3 bis 16 m Reichweite |
| **Skills** | **Skillpunkte hinzufügen**, **alle Skills auf einmal lernen** (ohne Münz-Ziel) |
| **Tools** | **Alle Items freischalten** (Werkzeuge, Beutel, Stiefel), Werkzeug-Reichweite, -Stärke und -Tempo – für jedes Werkzeug, auch die Hände |
| **Teleport** | Position speichern, zurück zum Start, Einzahl-Maschinen, Terminals, Aufzug und **jede Etage** |
| **Settings** | 6 Themes, **Menügröße**, Tasten ändern, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>F2</kbd> | Flugmodus an / aus |
| <kbd>F3</kbd> | Beutel einzahlen (egal wo du bist) |
| <kbd>F4</kbd> | Münz-Magnet an / aus |

Alle Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, bleiben deine Figur und die Kamera stehen. Ist ein Tab länger als das Fenster, scrollst du mit dem Mausrad.

**Flugmodus:** WASD, Leertaste nach oben, **Strg** nach unten, **Shift** schneller.

**Eigene Zahl eingeben:** Klick auf das dunkle Feld (z.B. *Custom amount*), tipp die Zahl ein und drück **Enter**.

**Save Settings:** im Tab *Settings*. Alle eingeschalteten Cheats und deine Einstellungen sind beim nächsten Spielen wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| Keine Mod lädt | `winhttp.dll` muss direkt neben `One Million Coins.exe` liegen, nicht in einem Unterordner. Achte darauf, dass du **BepInEx 5** für **win x64** geladen hast. |
| Menü sagt „Cheats are off“ | Starte oder lade zuerst einen Durchlauf – im Hauptmenü sind die Cheats aus. |
| F1 tut nichts | Prüfe, ob `EvilDragon.OneMillionCoinsCheats.dll` in `BepInEx\plugins\` liegt. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `BepInEx\LogOutput.log` an. |

## Deinstallieren

- **Nur die Cheats:** lösch `BepInEx\plugins\EvilDragon.OneMillionCoinsCheats.dll`.
- **Alles:** lösch zusätzlich `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` und den Ordner `BepInEx` aus dem Spielordner.

## Hinweise

- Eingezahlte Münzen, Skills und Skillpunkte bleiben in deinem Spielstand. Weil die Münzen echt aus dem Goldhaufen genommen werden, lädt dein Spielstand ganz normal.
- **Unlock All Items** ändert nichts am Spielstand – nach dem Neuladen hast du wieder nur deine eigenen Items (außer der Schalter ist an).
- **Bestenlisten:** Ein Durchlauf mit Cheats lädt keine Speedrun-Zeit und keine eingezahlten Münzen zu den Steam-Bestenlisten hoch – genau wie der Testmodus des Spiels. Dafür kann einmal der Hinweis *„Banking history needs recovery“* erscheinen, das ist normal.
- Wird das Menü zu groß oder zu klein angezeigt, stell im Tab *Settings* die **Menu Size** um.
- Steam-Erfolge können auch mit aktiven Cheats freigeschaltet werden.
- Deine Einstellungen liegen in `BepInEx\config\evildragon.cheats.onemillioncoins.cfg`.

<details>
<summary><b>Aus dem Quellcode bauen</b> (für Entwickler)</summary>

Voraussetzungen: .NET SDK 6.0 oder neuer und das Spiel mit installiertem BepInEx 5.

```powershell
cd games/OneMillionCoins/src
copy GameDir.props.example GameDir.props   # trag hier deinen Spielordner ein
dotnet build -c Release
```

Die DLL wird automatisch nach `BepInEx\plugins` kopiert (außer das Spiel läuft gerade).
</details>

## Changelog

**1.0.0**
- Erste Version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
