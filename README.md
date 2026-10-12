# vollmer-vsharp-game

Interaktives Arcade-Game der VOLLMER WERKE Maschinenfabrik GmbH (Biberach/Riß).

## Lokale Entwicklung

Das Repository enthält den lokalen ASP.NET-Core-Host, die Blazor-Betriebshülle, einen Phaser-/TypeScript-Spieler und einen passiven Watcher. Die serverautoritativen Runden-, Rollen-, Treffer- und Wertungsregeln laufen über SignalR. Im spielbaren Prototyp werden die ausgewählte Showroom-Bühne sowie ein Bohrer, ein Fräser und ein Sägeblatt verwendet. Bei fehlenden Bilddateien wird prozedural gezeichnet. Der vollständige Bildsatz, finale Zustands-Overlays, Zonen-Editor und generiertes Manifest stehen noch in den Konzepten 00002 und 00003 aus.

Voraussetzungen für den vollständigen Client-Build: .NET SDK `10.0.401`, Node.js `22.12.0` bis `24.x` und npm `11.19.0`.

```powershell
Push-Location src/VGame.Client
npm install
npm run build
Pop-Location
dotnet build VGame.sln
.\Start-Game.ps1
```

`Start-Game.ps1 -OpenWatcher` öffnet zusätzlich die passive Watcher-Ansicht. Der Host bindet standardmäßig ausschließlich an `127.0.0.1:5080`; `/health` liefert den Bereitschaftsstatus. Ohne zuvor gebauten Phaser-Client zeigen Spieler und Watcher einen klar gekennzeichneten Scaffold-Fallback.

`Publish-Game.ps1` erstellt nach dem Client-Build ein selbstständiges Windows-x64-Paket unter `artifacts/publish/win-x64`. Das Paket benötigt für den Start weder Internet noch NuGet- oder npm-Zugriff.

## Ausgewählter Artwork-Startsatz

Die unveränderten Leonardo-JPGs liegen unter `media-source/leonardo/`. Über `node media-source/prepare-artwork.mjs` werden nach Installation der Client-Abhängigkeiten Variante 8 der Bühne, Variante 1 des Bohrers, Variante 3 des Fräsers und Variante 1 des Sägeblatts aufbereitet. Die Originale werden nicht verändert.

Transparente Werkzeug-Master und die zugeschnittene Bühne werden unter `media-source/affinity/` abgelegt; der Ordnername bezeichnet die Master-Ablage, nicht eine manuelle Affinity-Bearbeitung. Die Laufzeitbilder werden unter `Assets/images/` erzeugt und ausschließlich über `/Assets/images/` ausgeliefert. Bohrer und Fräser werden mit nach rechts gerichteter Schneide ausgerichtet. Bild und serverseitige Prototypzonen werden zusammen in `Assets/config/artwork.prototype.json` zugeordnet; diese Datei ist ausdrücklich kein Produktionsmanifest.

Die Bühne wird auf 2560 × 1440 skaliert; dadurch werden keine zusätzlichen Bilddetails erzeugt. Die Freistellung erfolgt anhand des roten/pinken Hintergrunds. Die automatisch abgeleiteten Zonen, Alpha-Kanten in kleinster Spielgröße und die noch in die Zielfläche ragenden Maschinen benötigen die finale Sichtprüfung beziehungsweise Retusche. Werkzeugzustände werden vorläufig durch Tint und Beschriftung dargestellt, nicht durch die geplanten Zustands-Overlays. Die Sägeblattbohrung ist bildlich transparent, wird gemäß Konzept aber als Schaftzone gewertet.
