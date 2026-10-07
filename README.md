# vollmer-vsharp-game

Interaktives Arcade-Game der VOLLMER WERKE Maschinenfabrik GmbH (Biberach/Riß).

## Lokale Entwicklung

Das Repository enthält den lokalen ASP.NET-Core-Host, die Blazor-Betriebshülle, einen Phaser-/TypeScript-Spieler und einen passiven Watcher. Die serverautoritativen Runden-, Rollen-, Treffer- und Wertungsregeln laufen über SignalR. Der aktuelle Phaser-Spielclient ist ein spielbarer Prototyp mit prozedural gezeichneten Werkzeug-Platzhaltern; Produktionsbilder, Zustands-Overlays, Zonen-Editor und generiertes Manifest stehen noch in den Konzepten 00002 und 00003 aus.

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
