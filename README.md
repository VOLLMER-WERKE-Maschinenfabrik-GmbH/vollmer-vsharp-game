# vollmer-vsharp-game

Interaktives Arcade-Game der VOLLMER WERKE Maschinenfabrik GmbH (Biberach/Riß).

## Lokale Entwicklung

Das Repository enthält den lokalen ASP.NET-Core-Host, die Blazor-Betriebshülle, einen Phaser-/TypeScript-Spieler und einen passiven Watcher. Die serverautoritativen Runden-, Rollen-, Treffer- und Wertungsregeln laufen über SignalR. Im spielbaren Prototyp werden die ausgewählte Showroom-Bühne sowie ein Bohrer, ein Fräser und ein Sägeblatt verwendet. Bei fehlenden Bilddateien wird prozedural gezeichnet. Zonen-Editor und Manifest-Werkzeugkette sind vorhanden; vollständiger Produktionsbildsatz und die Spielanbindung des Produktionsmanifests stehen noch aus.

## Spielstand zu 00001/AP-04

Hintergrund und Spiel-Leinwand füllen den sichtbaren Bühnenrahmen ohne Letterboxing. Das Hintergrundbild wird zentriert mit `cover` beschnitten; die transparente 1280×720-Spiel-Leinwand wird mit Phaser `ENVELOP` proportional vergrößert und am Rahmen abgeschnitten. Dadurch liegen Werkzeug-Ein-/Austritte nicht mehr an einer inneren schwarzen Balkenkante. Abhängig vom Seitenverhältnis werden äußere Spielbereiche beschnitten; Werkzeugproportionen, Rotation und normalisierte Trefferkoordinaten bleiben unverändert. Spieler und Watcher verwenden dieselbe Skalierungsregel.

Werkzeuge fliegen einmalig über die gesamte Spielfläche: zufällig links nach rechts, rechts nach links, oben nach unten oder unten nach oben. Start und Ende liegen vollständig außerhalb der sichtbaren Leinwand, auch bei Rotation. Leicht variierende kubische Bézier-Bahnen werden in 6–9 Sekunden ohne Rücklauf durchlaufen; am Ende wird das Werkzeug ohne Punkteabzug entfernt. Werkzeuge dürfen sich überschneiden und werden nicht mehr durch Mindestabstand oder Flächenbelegung blockiert. Bei gleichzeitigem Kontakt gewinnt das sichtbare vordere Werkzeug; innerhalb desselben Werkzeugs bleibt der Schaftvorrang erhalten. Maximal sechs Ziele bleiben gleichzeitig registriert. Die gesamte Leinwand ist als Eingabefläche freigegeben.

Je Werkzeug werden Drehrichtung und konstante Drehgeschwindigkeit von 12–30°/s zufällig gewählt. Bewegung und Rotation werden ausschließlich im Server ausgewertet; Spieler und Watcher erhalten dieselben Positionen und Winkel. Trefferzonen und verlangte Säge-Swipe-Richtung drehen mit; die Mindestlänge bleibt unabhängig vom Winkel. Bestehende lineare Platzierungen bleiben als kompatibler Fallback möglich. Audio bleibt unverändert.

Seit 2026-10-26 werden die gelieferten Leonardo-Gesichter als 32 transparente Bild-Overlays verwendet: 12 stumpfe, 4 Schleif-, 8 scharfe und 8 defekte Varianten. `node media-source/prepare-faces.mjs` erzeugt aus den unveränderten PNGs unter `media-source/leonardo/faces/` proportionale verlustfreie WebP-Dateien mit transparentem Rand und Zustandskatalog unter `Assets/images/faces/`. Die Auswahl bleibt je Werkzeug-ID und Zustand stabil; separate Gesichtsobjekte verhindern Abschneiden an der Werkzeugkontur. Gezeichnete Gesichter werden nur noch bei fehlenden Bildvarianten verwendet.

Beim Sägeblatt reicht die Schärfzone nach innen bis zur schwarzen Kreislinie; nur der Bereich innerhalb dieses Kreises einschließlich Bohrung bleibt Schaft. `node media-source/prepare-artwork.mjs --tools-only` aktualisiert die Werkzeugbilder/-konturen ohne den inzwischen angepassten Hintergrund neu zu erzeugen. Für diese Zonenanpassung bleiben alle Runtime-Bildbytes sowie Bohrer-/Fräserdefinitionen unverändert.

Außerhalb einer laufenden Runde wird beim aktiven Spieler `Assets/audio/music/menu.mp3` ab Dateianfang bis Dateiende (2:32) geloopt, auch in der Ergebnisansicht. Die Browser-Audiofreigabe erfolgt mit der ersten bewussten Zeiger- oder Tastatureingabe; vorher bleibt das Menü gegebenenfalls stumm. Beim Rundenstart wird auf `Assets/audio/music/bgm.mp3` gewechselt, dabei wird das Intro übersprungen und ab 0:15 bis Dateiende (2:18) geloopt. Beim Rundenende wird wieder zur Menümusik gewechselt; beide Tracks laufen nie gleichzeitig. Musik ist auf -16 dB eingestellt, Kurzfeedback-Effekte auf Gain 0,2 (etwa -14 dB), Stimmen bleiben unverändert. Effekte senken Musik um 6 dB über 80 ms ab; nach 120 ms Haltezeit wird über 250 ms zurückgeblendet. F6 steuert alle Busse, Stumm lässt die Musik lautlos weiterlaufen; Rollenverlust und Verbindungsabbruch stoppen sie. Eine endgültige Hör-/Loopfreigabe sowie UI-/Jingledateien und vollständige Mischer-/Ausschnittkonfiguration stehen aus.

Die Werkzeuge tragen vier Comic-Mimikzustände passend zu stumpf, Schliff, scharf und defekt. Auf Nutzerwunsch werden Bohrer seit 2026-10-27 proportional 15 % größer dargestellt; Trefferzonen werden zusammen mit dem Werkzeug skaliert. Gesichter belegen bei Bohrern und Fräsern 32 % statt 42 % der Werkzeugbreite, bei Sägeblättern 42 % statt 52 %, damit sie weniger überstehen. Gesichter bleiben dekorativ und verändern keine Trefferzonen; ausschließlich die Schneidenzustände bleiben maskiert. Auf Nutzerfreigabe wurde die Wiedergabe der gelieferten 28 Stimmen-/Effektdateien aus AP-05 vorgezogen. Clips werden vorab geladen und nach bewusster Eingabe dekodiert; Treffergeräusche werden nur beim aktiven Spieler nach bestätigter Aktion abgespielt. Ein gemeinsamer Web-Audio-Mischer ersetzt den ungenutzten Phaser-Audiomanager; Watcher bleiben ohne Audiokontext und Audioabrufe.

Ein Klick ins Leere (`miss`) startet keine Werkstückstimme und keinen Schrei, sondern nur den Laser-/Schleifeffekt. Vorgemerkte Stimmen werden verworfen; der Punkteabzug bleibt erhalten. Fehlerstimmen werden nur durch tatsächliche Fehlbearbeitung beziehungsweise Schafttreffer ausgelöst.

F6 schaltet laut/leise/stumm. Je ein aktueller Kurzfeedback- und Sprachslot mit kurzem Ersatz-Fade, Shuffle-Beutel ohne direkte Wiederholung, Event-Deduplizierung, zeitlich begrenztes jüngstes Sprachereignis und Runden-/Rollenschutz verhindern wachsende Soundwarteschlangen oder historische Wiedergabe. Audiofehler werden sichtbar gemeldet; fehlt der Effekt, bleibt ein kurzer Ersatzton. Clips werden nicht automatisch gepitcht oder willkürlich gekürzt. Die gelieferten Dateien sind jeweils 1,625 Sekunden lang. Musik, UI-/Jingledateien, Ducking und hörgeprüfte konfigurierbare Ausschnitte stehen weiterhin aus.

Benötigte Nutzerabnahme: Nach Start mehrere Schneiden-/Schafttreffer und Säge-Swipes ausführen, Tonpegel/F6 und Stimmenzuordnung am tatsächlichen Gerät prüfen. Bei auffälliger Lautheit, Stille, Verzerrung oder ungeeignetem Ausschnitt bitte die konkrete Datei beziehungsweise Situation nennen; zusätzlich Mimiklesbarkeit auf dem Messedisplay bestätigen. Die Browser-/HTTP-Prüfungen sind keine Hör- oder Audio-Latenzfreigabe. Die neue Entwicklungsansicht ist auf Port 5081 verfügbar; das alte Publish-Paket bleibt bis zum erneuten Publish/Neustart unverändert.

Die drei vorhandenen Werkzeugkonturen decken alle opaken Bildpixel ab; die freigegebenen Bildbytes bleiben unverändert. Ein direkter Tap oder Swipe-Start in einer kleinen Schneide wird nicht mehr durch einen bloßen Schaft-Toleranzkontakt verdrängt. Gemeinsame Zonengrenzen behalten den Schaftvorrang, außerhalb gestartete Swipes ihre bisherige Kontaktfolge.

Schneidenzustände werden zur Laufzeit als am Bildalpha und der `cut`-Zone maskierte Texturen gezeichnet, ohne den Schaft einzufärben. Erfolg zeigt kurz den Schleifübergang, anschließend eine helle grüne Schneide; Fehler markieren die Schneide rot mit dunklen Spuren. Diese Darstellung ersetzt keine behauptete Lieferung der zwölf Produktions-Overlaydateien. Sofortige Gestenspur, bestätigte Ergebnisbeschriftung, Pointer-Abbruch und Feedbackbereinigung an Rundengrenzen sind umgesetzt. Die Ergebnisanzeige liegt vor den Zielen.

Die Bühnenfläche stammt jetzt aus dem Server-Snapshot; bestätigte Kontaktpunkte werden an Spieler und Watcher übertragen. SignalR nutzt den verbindlichen Outcome-Vertrag `shaft_hit`/`wrong_sharpening`. Das Canvas folgt der Größe seines Spielbereichs. Der aktuelle Entwicklungsstand ist unter `http://127.0.0.1:5081/play` und `/watcher` erreichbar, sofern der Entwicklungsserver läuft. Ein älteres Publish-Paket wird dadurch nicht aktualisiert.

Abnahme dieses Schritts: vollständige Suite 155 Tests bestanden, Client-Build erfolgreich, reale Bild-/Katalogabdeckung geprüft; Browser-Laser-/Sägeerfolg, Schaftabzug, Pointer-Abbruch, vier unterschiedliche Schneidentexturen mit unverändertem Schaft und synchroner passiver Watcher geprüft. Verborgene integrierte Browsertabs erforderten für Teile der Grafik-/Resizeprüfung einen reinen Testhook; daraus wird keine Hardware-Latenz- oder DPI-Freigabe abgeleitet. 00001/AP-04 bleibt offen bis zur vollständigen Bild-/PKD-/Requisitenlieferung, freien Bühnenfläche, Produktionsmanifest-Anbindung und Zielhardware-Abnahme. Weitere Arbeiten an 00003 wurden zugunsten des Spiels zurückgestellt.

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

Die HTML-Einstiegspunkte `/play` und `/watcher` werden mit `Cache-Control: no-store` ausgeliefert, damit neue Paketstände keine alten Bundle-Verweise aus dem Browsercache übernehmen. Nach einem Publish muss der laufende Paketserver neu gestartet werden. Bereits vor dieser Korrektur zwischengespeicherte Seiten werden einmal mit `Strg+F5` neu geladen oder über einen zusätzlichen Versionsparameter geöffnet; gehashte JavaScript-/CSS-Dateien behalten ihre bestehenden Cache-Regeln.

## Retro-Maschinenhülle und Ereignistasten

Die Spieleransicht enthält die Retro-Hülle mit Status-/Achsspalte, zwei Maschinenportraits, acht grünen Ereignistasten und F1–F6-Leiste. Start und Moduswahl werden durch bestätigte Server-Snapshots freigegeben; während einer Runde bleibt die Moduswahl gesperrt und Start/F1 wird zu „Abbrechen“. Der aktive Spieler kann vorzeitig zur Ergebnisansicht wechseln und sofort neu starten; Punktestand und Spielerrolle bleiben dabei erhalten. Abgebrochene Runden werden nicht in die Rangliste übernommen. F4 zeigt eine visuelle Gesten-Demo, F5 öffnet die Bestenliste und F6 regelt laut, leise und stumm. In der Watcheransicht werden weder bedienbare Besucherleisten noch Formulare für Spielerdaten angezeigt.

### Spielerdaten und Highscores

Vor jeder Runde wird beim aktiven Spieler eine CAM-inspirierte Maske mit schwarzer Fläche, Monospace-Schrift, grünen Eingabefeldern und blauer Befehlsleiste geöffnet. Ein Name oder Fantasiename mit Ranglisteneinwilligung genügt für den Highscore. Die E-Mail ist freiwillig und wird nur zusammen mit der gesonderten Zustimmung zur Gewinnbenachrichtigung angenommen. „Ohne Eintrag spielen“ startet ohne personenbezogene Speicherung. Beim Schließen beziehungsweise Start werden die Formularwerte verworfen; sie werden nicht im Browser-Storage abgelegt.

Nur vollständig gespielte, registrierte Runden werden mit serverseitig bestimmtem Modus und Punktestand gespeichert. Die getrennten Bestenlisten für Laser und Scheibe enthalten öffentlich nur Anzeigename, Rang, Punktestand und Zeitpunkt. E-Mail und Einwilligungsnachweis bleiben privat. Nach regulärem Rundenende wird die Bestenliste geöffnet; der zugehörige aktive Spieler erhält zusätzlich einen Löschcode und kann seinen Eintrag sofort löschen. Nach Rollenübergabe wird der private Code nicht mehr an den bisherigen Client ausgegeben. Der Watcher erhält keine Kontaktdaten oder Löschcodes.

Gespeichert wird standardmäßig atomar in `%LOCALAPPDATA%/Vollmer/VGame/highscores.json`, außerhalb von Webroot und Repository. Die Datei enthält vertrauliche Daten und darf weder öffentlich freigegeben noch in Diagnose-Uploads oder Quellcode übernommen werden. `PlayerData:StoragePath` kann serverseitig überschrieben werden; es darf nur ein Host auf dieselbe Datei zugreifen. Kontaktadresse, Hinweisversion und Zeitzone stehen unter `PlayerData` in der Serverkonfiguration; Änderungen erfordern einen Hostneustart.

Auf Nutzerentscheidung werden Ranglisteneintrag, E-Mail und Nachweise am folgenden Kalendertag um 00:00 Uhr in `Europe/Berlin` gelöscht. Bei laufendem Host erfolgt die Bereinigung automatisch, nach einer Betriebspause beim nächsten Zugriff beziehungsweise Start des Rundendienstes. **Gewinnbenachrichtigungen müssen daher noch am Spieltag erfolgen.** Für eine spätere Benachrichtigung nach der Messe muss die Aufbewahrungsregel vor der Erfassung angepasst werden. Automatischer Mailversand, vollständige Gewinnspielbedingungen, geschützter Betreiberexport und Vertriebsfreigaben sind nicht implementiert. Der Datenschutzhinweis muss vor dem realen Messebetrieb organisatorisch geprüft und bei Bedarf vervollständigt werden.

Die Ereignisprofile werden in `Assets/config/gameplay.json` unter `workshopEvents` eingestellt:

| Einstellung | Wirkung |
| --- | --- |
| `enabled` | Alle Werkstatt-Spielereien werden gemeinsam aktiviert oder deaktiviert. |
| `events.<name>.enabled` | Die einzelne Symboltaste wird aktiviert oder deaktiviert. |
| `events.<name>.durationMilliseconds` | Effektdauer von 1 bis 2000 ms; Standardwert 1400 ms. |
| `events.<name>.cooldownMilliseconds` | Wartezeit nach dem Effekt von 0 bis 60000 ms; Standardwert 3000 ms. |

Die Profilnamen sind `gauge`, `conveyor`, `wiper`, `stamp`, `tool`, `shield`, `drive` und `egg`. Während eines Effekts werden alle Ereignistasten gegen Überlagerung gesperrt. Danach bleiben nur noch Tasten mit laufendem Cooldown oder deaktiviertem Profil gesperrt. Sämtliche Effekte sind lokal und wertungsneutral; Ereignistöne und eine konfigurierte Easter-Egg-Sequenz sind noch nicht implementiert.

Über `GET /api/workshop-events` werden ausschließlich die validierten Besucheroptionen ohne Cache ausgeliefert. Dateiänderungen werden vom Host neu eingelesen; die Spielseite muss anschließend neu geladen werden, damit die Profile übernommen werden. Ein Client-Neubau ist dafür nicht erforderlich. Ungültige Startkonfiguration verhindert den Hoststart; bei ungültigen Laufzeitwerten liefert der Endpunkt HTTP 503. Bei fehlender oder ungültiger Antwort bleiben die Ereignistasten gesperrt und ein Fehlerhinweis wird angezeigt, ohne den Rundenstart zu verhindern. Im veröffentlichten Paket muss dessen eigene Konfigurationsdatei geändert werden.

## Manifest- und Authoring-Grundlage

Die typisierten Manifest-/Metadaten-/Zonenverträge stehen in `src/VGame.Contracts/AssetManifest.cs`. Die JSON-Feldnamen aus dem Konzept bleiben erhalten; Polygonpunkte werden im Autorenmanifest als Zahlenpaare gespeichert, nicht als Gesten-`Point2`-Objekte. Die sieben Kategorievorlagen in `Assets/templates/` verwenden JSON Schema Draft 2020-12 und referenzieren gemeinsame Pflichtangaben für Herkunft, Lizenz und Erstellungszeit. Bei Leonardo-Herkunft werden zusätzlich tatsächliche Angaben zu Modell, Seed und Prompt verlangt; Produktionswerte werden nicht erfunden.

`media-source/plan/delivery-list.csv` und das Bootstrap-Register `Assets/config/id-register.json` enthalten die 37 geplanten Asset-IDs. Alle sind reserviert; die Reservierung ist kein Liefer- oder Abnahmenachweis. Metadaten werden später unter `media-source/assets/`, Zonendateien unter `media-source/zone-authoring/` erzeugt. Das Register wird anschließend durch die Werkzeugkette gepflegt, nicht durch manuelle Neuvergabe.

Manifest-Build/Verify und automatische Audiopool-Ableitung sind unter `tools/` implementiert und in der Solution enthalten. Der lokale Zonen-Editor ist im Debug-/Development-Host verfügbar. Der laufende Prototyp konsumiert weiterhin `artwork.prototype.json`; das neue Manifest wird noch nicht als Spielkonfiguration übernommen.

```powershell
dotnet build tools/VGame.Tools.Cli/VGame.Tools.Cli.csproj
dotnet tools/VGame.Tools.Cli/bin/Debug/net10.0/vgtools.dll manifest build --assets Assets --source media-source --out Assets/config/manifest.json
dotnet tools/VGame.Tools.Cli/bin/Debug/net10.0/vgtools.dll manifest verify --assets Assets --source media-source --out Assets/config/manifest.json
dotnet tools/VGame.Tools.Cli/bin/Debug/net10.0/vgtools.dll audio pools build --assets Assets --source media-source --out Assets/config/manifest.json
dotnet test tools/VGame.Tools.Manifest.Tests/VGame.Tools.Manifest.Tests.csproj --filter "FullyQualifiedName~VGame.Tools.Manifest.Tests"
```

Der Build erzeugt ausschließlich `manifest.json`, `manifest.md` und das sortierte ID-Register unter `Assets/config/`; Bilder und Autorenquellen bleiben unverändert. Die Ausgabe ist bei unveränderten Eingaben byteidentisch. Prüfsummen, Dateiformat/-maß, Transparenzrand, Silhouettenabdeckung, gültige Polygongeometrie und Bühnenfläche werden geprüft. Die nach Schaftvorrang verbleibende Schneide muss zusammenhängend sein und opake Trefferpixel enthalten. Metadaten dürfen erzeugte Dateipfade oder Prüfsummen nicht überschreiben. Aktive Werkzeuge benötigen tatsächlich gelieferte Zustands-Overlays und gültige Audioverweise.

`Assets/config/audio.pools.json` enthält nur Pool-ID, Dateigruppe, Kategorie, Lautstärkeprofil, Beutelregel und Fallback, keine `variants`-Dateiliste. Bei jedem Build/Verify werden vorhandene WAV-/MP3-/OGG-Dateien unter `Assets/audio/**` nach `<fileGroup>_<nn>.<format>` mit Nummern 01–99 abgeleitet und sortiert. Doppelte Nummern über Ordner/Formate, ungültige Dateinamen, unbekannte Fallbacks und Fallback-Zyklen verhindern die Ausgabe. Leere Pools bleiben mit ihrer Fallback-Zuordnung erhalten; Dateien oder Ersatzclips werden nicht erfunden. Änderungen des Dateibestands werden beim schreibfreien Verify als veraltete Ausgabe erkannt.

`audio pools build` verwendet denselben vollständigen Manifest-Build wie `manifest build`; es wird keine zweite Pooldatei mit Dateilisten gepflegt und kein fremder Manifestinhalt partiell überschrieben. Der aktuelle Bestand ergibt sechs Pools mit 28 unveränderten Varianten: Laser-Erfolg 4, Laser-Fehler 5, Lasereffekt 6, Säge-Erfolg 4, Säge-Fehler 5, Schleifeffekt 4. Die Manifest-Tabelle führt Pools, Varianten und Fallbacks zum Gegenlesen auf. Die daraus gelieferte Stimmen-/Effektwiedergabe samt Beutel und F6 ist inzwischen auf Nutzerfreigabe im Spiel angebunden. Musik, Ducking, UI/Jingles und Hör-/Lautheitsfreigabe bleiben offen.

Geplante Reservierungen ohne vollständige zugewiesene Metadaten erscheinen als `planned`, ohne tote Dateiverweise oder erfundene Herkunftsdaten. Dafür kann `build` eine gegenlesbare Aufstellung mit Exit-Code `0` erzeugen; `verify` bleibt mit Exit-Code `3` rot und schreibt nichts. Ungültige aktive Einträge, doppelte IDs, aktive reservierte IDs oder ein von Hand verändertes geschütztes Manifest führen zu Exit-Code `2`; fehlende aktive Dateien zu `3`. Bei Validierungsfehlern wird die vorhandene Ausgabe nicht ersetzt. Die Manifest-Prüfsumme schützt semantische Handänderungen; absichtlich neu berechnete Prüfsummen sind kein Sicherheitsmodell. Die drei Ausgabedateien werden einzeln atomar ersetzt, nicht als gemeinsame Dateisystemtransaktion.

Im aktuellen Repositorybestand sind weiterhin alle 37 Produktions-IDs geplant: vollständige Produktionsmetadaten, Zonen und Bild-/Overlaylieferungen fehlen. Die bereits eingebundenen vier Prototypbilder werden dadurch nicht als fehlend oder freigegeben umgedeutet. Build wurde erfolgreich ausgeführt, ein zweiter Build war byteidentisch; Verify bestätigte Exit-Code `3` ohne Schreibzugriff. Das ist noch keine Produktions- oder Hardware-Abnahme. Eine lokale Ignore-Ausnahme in `media-source/.gitignore` erlaubt die Versionierung von Metadaten, Zonendateien und Lieferliste; Leonardo-Rohbilder und Affinity-Master bleiben ausgeschlossen.

## Lokaler Zonen-Editor

```powershell
dotnet run --project src/VGame.Server/VGame.Server.csproj --configuration Debug --launch-profile http -- --Host:Urls=http://127.0.0.1:5081
```

Unter `http://127.0.0.1:5081/tools/zones` werden Werkzeugbilder ausgewählt und normalisierte `cut`-/`shaft`-Polygone sowie der Pivot bearbeitet. Punkte werden per Mausklick angelegt, gezogen oder numerisch geändert; Löschen, Zonenleeren, Undo/Redo, Zoom und Einpassen sind verfügbar. Ungedeckte opake Pixel werden rot markiert. Speichern bleibt bis zur Bestätigung von Abdeckung und Geometrie durch den gemeinsamen Servervalidator gesperrt.

Geschrieben werden ausschließlich `media-source/zone-authoring/<assetId>.zones.json`, keine Bilder, Metadaten oder Manifeste. Ein Versionskonflikt liefert HTTP 409 statt fremde Änderungen zu überschreiben. Die Tap-/Swipe-Probe verwendet eine isolierte `GameEngine` mit aktueller Gameplay-Konfiguration im normalisierten Werkzeugraum, ohne die aktive Runde zu verändern. Sie ersetzt keine spätere Größenklassen-/DPI-/Latenzabnahme. GET-Swipe-Proben benötigen `endX`/`endY`; POST-Proben prüfen die aktuelle ungespeicherte Zonenfassung.

Werkzeugseite, API und direkte Editorressourcen sind nur für lokale Debug-/Development-Aufrufe erreichbar; Fernzugriff, fremde Origins und Cross-Site-Aufrufe werden abgewiesen. Die Toolbibliothek ist vom Release-Host ausgeschlossen, Editor-HTML/-JS/-CSS werden nicht veröffentlicht. Das Messepaket stellt keinen Editor bereit. Vorhandene Prototypzonen bleiben ungeprüfte Entwürfe mit sichtbaren Lücken; Browser-Testpolygone wurden nicht als Produktionszonen gespeichert.

## Ausgewählter Artwork-Startsatz

Die unveränderten Leonardo-JPGs liegen unter `media-source/leonardo/`. Über `node media-source/prepare-artwork.mjs` werden nach Installation der Client-Abhängigkeiten Variante 8 der Bühne, Variante 1 des Bohrers, Variante 3 des Fräsers und Variante 1 des Sägeblatts aufbereitet. Die Originale werden nicht verändert.

Transparente Werkzeug-Master und die zugeschnittene Bühne werden unter `media-source/affinity/` abgelegt; der Ordnername bezeichnet die Master-Ablage, nicht eine manuelle Affinity-Bearbeitung. Die Laufzeitbilder werden unter `Assets/images/` erzeugt und ausschließlich über `/Assets/images/` ausgeliefert. Bohrer und Fräser werden mit nach rechts gerichteter Schneide ausgerichtet. Bild und serverseitige Prototypzonen werden zusammen in `Assets/config/artwork.prototype.json` zugeordnet; diese Datei ist ausdrücklich kein Produktionsmanifest.

Die Bühne wird auf 2560 × 1440 skaliert; dadurch werden keine zusätzlichen Bilddetails erzeugt. Die Freistellung erfolgt anhand des roten/pinken Hintergrunds. Die opake Konturabdeckung der drei Prototypbilder wurde lückenlos korrigiert und getestet; Alpha-Kanten in kleinster Spielgröße und die noch in die Zielfläche ragenden Maschinen benötigen weiterhin finale Sichtprüfung beziehungsweise Retusche. Werkzeugzustände werden mit schneidenspezifischen Laufzeitmasken, Übergang und Beschriftung dargestellt, nicht durch gelieferte Produktions-Overlaydateien. Die Sägeblattbohrung ist bildlich transparent, wird gemäß Konzept aber als Schaftzone gewertet.
