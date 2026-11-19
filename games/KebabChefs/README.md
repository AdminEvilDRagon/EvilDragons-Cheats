<img src="https://capsule-render.vercel.app/api?type=rect&color=0:00ff4c,100:04140a&height=90&text=Kebab%20Chefs!%20Prologue&fontColor=ffffff&fontSize=40&fontAlignY=55" width="100%" />

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/DE-Deutsch-00ff4c?style=for-the-badge&labelColor=0b1a10" alt="Deutsch" /></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/EN-English-1c2a22?style=for-the-badge&labelColor=0b1a10" alt="English" /></a>
</p>

<p align="center">
  <img src="../../assets/logo.png" width="110" alt="EvilDragon Logo" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-00ff4c?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/Unity-IL2CPP-00e5ff?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/BepInEx-6%20(be.793)-ff2fbf?style=for-the-badge&labelColor=0b1a10" />
  <img src="https://img.shields.io/badge/plattform-Windows-ffb300?style=for-the-badge&labelColor=0b1a10" />
</p>

<p align="center"><b>EvilDragon's Cheats</b> – ein Cheat-Menü im Spiel für <i>Kebab Chefs! - Restaurant Simulator Prologue</i>.</p>

> **Nur Einzelspieler.** Kebab Chefs hat Online-Koop. Sobald ein anderer Spieler in deiner Sitzung ist (oder du bei jemand anderem spielst), schalten sich alle Cheats automatisch ab.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Installation

Die Installation dauert etwa 5 Minuten und muss nur einmal gemacht werden.

### Schritt 1 – Spielordner öffnen

1. Öffne **Steam** und geh in deine **Bibliothek**.
2. Rechtsklick auf **Kebab Chefs! - Restaurant Simulator Prologue** → **Verwalten** → **Lokale Dateien durchsuchen**.
3. Ein Ordner öffnet sich. Darin siehst du die Datei `Kebab Chefs! - Restaurant Simulator Prologue.exe`. **Lass diesen Ordner offen.**

### Schritt 2 – BepInEx installieren (der Mod-Loader)

1. Öffne die BepInEx-Downloadseite: <https://builds.bepinex.dev/projects/bepinex_be>
2. Ganz oben steht der neueste Build (Nummer **793 oder höher**). Lade die Datei
   **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.XXX....zip`** herunter
   (es muss **Unity.IL2CPP** und **win-x64** drinstehen – die anderen Dateien funktionieren nicht).
3. Öffne die ZIP und kopiere **alles darin** in deinen Spielordner aus Schritt 1.
4. Kontrolle: Neben der `.exe` liegen jetzt die Datei `winhttp.dll` und der Ordner `BepInEx`.

### Schritt 3 – Spiel einmal starten

1. Starte das Spiel ganz normal über Steam.
2. Neben dem Spiel öffnet sich ein schwarzes Konsolenfenster. **Das ist normal.**
3. Der erste Start dauert **2–5 Minuten**, weil BepInEx sich einrichtet. Warte, bis das Hauptmenü erscheint.
4. Schließ das Spiel.

### Schritt 4 – Cheats installieren

1. Geh auf die [**Releases**](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/releases)-Seite und lade
   **`EvilDragons-Cheats-KebabChefs-v1.0.0.zip`** herunter (nimm immer die neueste Version).
2. Öffne die ZIP und kopiere den Ordner `BepInEx` darin in deinen Spielordner.
   Wenn Windows fragt, ob zusammengeführt/ersetzt werden soll, klick auf **Ja**.
3. Kontrolle: Die Datei `EvilDragon.KebabChefsCheats.dll` liegt jetzt in `BepInEx\plugins\`.

### Schritt 5 – Spielen

Starte das Spiel, lade dein Restaurant und drück **F1**. Das Menü öffnet sich links.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Features

| Tab | Was du machen kannst |
|:--|:--|
| **Player** | Lauftempo, Sprunghöhe, Zeit einfrieren, Tagesgeschwindigkeit, Zutaten verderben nie, **Instant Interact** (nichts mehr gedrückt halten, Ladebalken 20x schneller), **Blackjack-Helfer** (zeigt HIT oder STAY), **Lucky Cards** (du bekommst die beste Karte, der Dealer die schlechteste) |
| **Money** | Geld hinzufügen / abziehen / setzen, Einnahmen-Multiplikator bis x100, Festival- & Event-Münzen, alle Rechnungen & Kredite abbezahlen |
| **Restaurant** | XP & Level, Kochpunkte, jeden Perk einzeln oder alle gratis auf Maximum |
| **Items** | Jede Zutat und jedes Item spawnen (1–50 Stück) – mit Suche |
| **Settings** | 6 Themes, Tasten ändern, Save Settings |

## Hotkeys

| Taste | Aktion |
|:--:|:--|
| <kbd>F1</kbd> | Menü öffnen / schließen |
| <kbd>F2</kbd> | Ausgewählten Geldbetrag hinzufügen |

Beide Tasten kannst du im Tab **Settings** ändern. Solange das Menü offen ist, bleibt deine Figur stehen und die Kamera dreht sich nicht.

**Items suchen:** Klick ins Suchfeld und tipp los. Klick irgendwo anders hin, wenn du fertig bist. Mit **Strg+A** markierst du den Suchbegriff – Rücktaste löscht ihn, Weitertippen ersetzt ihn.

**Save Settings:** im Tab *Settings*. Alle eingeschalteten Cheats sind beim nächsten Spielen wieder an.

<img src="https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png" width="100%" />

## Probleme?

| Problem | Lösung |
|:--|:--|
| Kein schwarzes Konsolenfenster | BepInEx ist nicht richtig installiert. `winhttp.dll` muss direkt neben der `.exe` liegen, nicht in einem Unterordner. Achte darauf, dass du die Version **Unity.IL2CPP win-x64** geladen hast. |
| F1 tut nichts | Prüfe, ob `EvilDragon.KebabChefsCheats.dll` in `BepInEx\plugins\` liegt und ob du das Spiel vorher einmal gestartet hast (Schritt 3). |
| Menü sagt „Cheats are disabled“ | Andere Spieler sind in der Sitzung. Spiel allein. |
| Menü weg nach einem Spiel-Update | Lösch den Ordner `BepInEx\interop` und starte das Spiel neu – BepInEx baut ihn neu auf. |
| Etwas anderes | Erstell ein [Issue](https://github.com/AdminEvilDRagon/EvilDragons-Cheats/issues) und häng `BepInEx\LogOutput.log` an. |

## Deinstallieren

- **Nur die Cheats:** lösch `BepInEx\plugins\EvilDragon.KebabChefsCheats.dll`.
- **Alles:** lösch zusätzlich `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` und die Ordner `BepInEx` und `dotnet` aus dem Spielordner.

## Hinweise

- Geld, XP, Kochpunkte, Perks und gespawnte Items bleiben in deinem Spielstand.
- Steam-Erfolge können auch mit aktiven Cheats freigeschaltet werden.
- Deine Einstellungen liegen in `BepInEx\config\evildragon.cheats.kebabchefs.cfg`.

<details>
<summary><b>Aus dem Quellcode bauen</b> (für Entwickler)</summary>

Voraussetzungen: .NET SDK 6.0 oder neuer und das Spiel mit installiertem und einmal gestartetem BepInEx.

```powershell
cd games/KebabChefs/src
copy GameDir.props.example GameDir.props   # trag hier deinen Spielordner ein
dotnet build -c Release
```

Die DLL wird automatisch nach `BepInEx\plugins` kopiert (außer das Spiel läuft gerade).
Dieses Spiel hat GUILayout komplett entfernt – das Menü nutzt deshalb das eigene Layout aus `shared/EvilDragonUi.cs` (`Ui.Manual`).
</details>

## Changelog

**1.0.0**
- Erste Version

<img src="https://capsule-render.vercel.app/api?type=waving&color=0:04140a,50:00b341,100:00ff4c&height=90&section=footer" width="100%" />
