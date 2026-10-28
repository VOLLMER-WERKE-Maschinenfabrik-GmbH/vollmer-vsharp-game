# Vollmer VSharp

Lokales Arcade-Spiel für den Messeauftritt der VOLLMER WERKE Maschinenfabrik GmbH. Der aktive Spieler bearbeitet Werkzeuge per Tap oder Swipe; ein passiver Watcher kann die Runde auf einem zweiten Bildschirm verfolgen.

## Projektstatus

Das Repository enthält einen spielbaren Prototyp mit Laser- und Scheibenmodus, Attract-/Demo-Zyklus, lokalen Bestenlisten, Audiofeedback und einer Retro-Maschinenhülle. Runde und Wertung sind serverautoritativ; der Browser benötigt für den Spielbetrieb keine Internetverbindung.

Das Spiel ist noch nicht messefertig: Der vollständige Produktionsbildsatz, finale Zonen und Overlays, die Laufzeitübernahme des Produktionsmanifests sowie Abnahmen auf der Zielhardware stehen aus. Der verbindliche Arbeitsstand steht im [Hauptfortschritt](.documentation/.inbox/00001-p-vollmer-messe-game.progress.md).

## Schnellstart

Voraussetzungen für Entwicklung und Paketbau: .NET SDK `10.0.401`, Node.js `22.12.0` bis `24.x` und npm `11.x`.

Spieler starten:

```powershell
.\Start-Game.ps1
```

Mit passiver Watcher-Ansicht:

```powershell
.\Start-Game.ps1 -OpenWatcher
```

`Start-Game.ps1` verwendet ein vorhandenes Windows-Paket, falls eines vorhanden ist. Für Änderungen am Quellcode die Entwicklungsansicht direkt starten:

```powershell
npm --prefix src/VGame.Client ci
npm --prefix src/VGame.Client run build
dotnet run --project src/VGame.Server/VGame.Server.csproj --launch-profile http
```

Die lokale Spieleransicht ist unter `http://127.0.0.1:5080/play`, der Watcher unter `/watcher` erreichbar. Der Host bindet standardmäßig nur an `127.0.0.1`.

## Bedienung

- `F1`: Runde starten oder als aktiver Spieler abbrechen.
- `F2` / `F3`: Laser- beziehungsweise Scheibenmodus wählen, außerhalb einer laufenden Runde.
- `F4`: Textanleitung öffnen.
- `F5`: Bestenliste öffnen.
- `F6`: Audio zwischen laut, leise und stumm umschalten.

Im **Lasermodus** auf die Schneide tippen und den Schaft vermeiden. Im **Scheibenmodus** außerhalb beginnen, die Schneidkante streifen und den Swipe außerhalb beenden. Ein Treffer im Blattkörper macht die Scheibe defekt; Richtung und Swipe-Länge sind unerheblich.

Die grünen Werkstatttasten lösen kurze, lokale Effekte ohne Wertungswirkung aus. Die Überraschung wird nach drei verschiedenen Aktionen freigeschaltet. Die Spielansicht zeigt außerdem eine Laser-/Scheiben-Demo im Attract-Zyklus.

## Build und Tests

```powershell
dotnet build VGame.sln
dotnet test VGame.sln
```

Ein selbstständiges Windows-x64-Paket wird erstellt mit:

```powershell
.\Publish-Game.ps1
```

Die Ausgabe liegt unter `artifacts/publish/win-x64`. Für Änderungen im Paket muss der Paketbau erneut ausgeführt und der Host neu gestartet werden.

## Konfiguration und Werkzeuge

Rundenlänge, Punkte, Mindestzielzahl und Werkstatteffekte stehen in [gameplay.json](Assets/config/gameplay.json). Die Mindestzielzahl beträgt derzeit vier; es gibt keine Obergrenze. Das Spiel füllt den Bestand während einer Runde schrittweise nach.

Der lokale Zonen-Editor ist nur im Debug-/Development-Host unter `/tools/zones` verfügbar. Manifest-Build und Validierung liegen unter `tools/VGame.Tools.Cli`; das laufende Spiel verwendet derzeit noch `artwork.prototype.json`, nicht das Produktionsmanifest.

Der aktuelle Manifest-Build ist durch eine nicht registrierte Laufzeitdatei (`grind_spark1`) blockiert. Die generierte `manifest.json` wurde deshalb nicht manuell angepasst.

## Projektdokumente

- [Spielkonzept und Implementierungsplan](.documentation/.inbox/00001-c-vollmer-messe-game.concept.md)
- [Fortschritt des Hauptprojekts](.documentation/.inbox/00001-p-vollmer-messe-game.progress.md)
- [Asset- und Bildlieferung](.documentation/.inbox/00002-c-vollmer-assetlieferung.concept.md) und [Fortschritt](.documentation/.inbox/00002-p-vollmer-assetlieferung.progress.md)
- [Manifest und Hilfstools](.documentation/.inbox/00003-c-vollmer-manifest-und-hilfstools.concept.md) und [Fortschritt](.documentation/.inbox/00003-p-vollmer-manifest-und-hilfstools.progress.md)