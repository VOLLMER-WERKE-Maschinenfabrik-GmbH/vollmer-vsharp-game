# Vollmer Messe-Game
| | |
|-|-|
| **Erstelldatum:** | 2026-09-09 |
| **Letzte Änderung:** | 2026-10-11 |
| **Issue:** | 00001 |

## Commit-Vorschlag für den finalen Gesamt-Commit

> **Status:** `vorläufig` — Der Vorschlag wird nach Umsetzung, Einarbeitung aller Code-Review-Findings und erfolgreicher finaler Review anhand der tatsächlich enthaltenen Änderungen aktualisiert. Es wird kein separater Konzept-Commit erstellt.

```text
[00001] Interaktives Vollmer Messe-Game bereitstellen

- Geplante Umsetzung: humorvolles Touch-Arcade-Spiel auf einem Windows-Convertible mit lokaler Kabelanzeige und optionalem Beobachter.
- Geplante Entscheidung: autoritativer ASP.NET-Core-Server, Phaser/TypeScript-Spielclients und Blazor-Betriebshülle.
- Geplante Absicherung: automatisierte Regeltests sowie Touch-, Latenz-, Last- und Hardware-Abnahme.
- Nachweise vor Messebetrieb: konkrete Serienkonfiguration, Monitor-/Audiofunktion, Showroom-Bühnenbild mit freier Zielfläche sowie Lieferung und Abnahme der 37 Assets aus 00002 und der Produktionssounds aus Kap. 3.4.4.
```

# 0. Entscheidungsvorlage

| | |
|-|-|
| **Problem** | Ein selbstironisches Retro-Fungame soll am Messestand zuverlässig ohne WLAN spielbar sein. |
| **Lösung** | Fujitsu LifeBook U9313X als Windows-Convertible mit lokalem ASP.NET-Core-Server und Phaser/TypeScript-Client; ein 27-Zoll-QHD-Bildschirm zeigt kabelgebunden den passiven Watcher. |
| **Entscheidungsbedarf** | Keine offene Konzeptentscheidung. Mediengrundlage, Lead-/Gewinnspielzweck, Hardwareprofil und Assetvertrag sind geklärt; Betriebs- und Mediennachweise folgen erst vor Messebetrieb. |
| **Top-Risiko** | Ungeprüfte Touch-, Monitor- und Audioanschlüsse gefährden den verbindlichen Kabel-Fallback. |
| **Blockierende offene Punkte** | Keine für die Implementierung. Vor Messebetrieb müssen B-01 Hardware-/Audiofunktion und B-02 Medienlieferung nachgewiesen sein. |

# 1. Kontext, Zielsetzung & Use Case

## 1.1 tl;dr
- Das Spiel soll binnen weniger Sekunden zum Mitmachen motivieren und Vollmers Kompetenz bei Schleifen, Schärfen, Erodieren und Laserbearbeitung verständlich machen.
- Eine Runde dauert standardmäßig $60\text{ s}$; $30$ bis $90\text{ s}$ bleiben als konfigurierter, abnahmefähiger Rahmen zulässig.
- Ein Windows-Convertible ist Host und einziger aktiver Touch-Eingabepunkt; ein kabelgebundener Bildschirm zeigt die Runde passiv. Ein zusätzlicher Netzwerk-Beobachter ist optional, nicht betriebsnotwendig.

## 1.2 Ziele und Nicht-Ziele

| Ziel | Beschreibung |
|-|-|
| Z-01 | Messebesucher erkennen ohne Einweisung den Unterschied zwischen präzisem Laser-Tap und kraftvollem Schleif-Swipe. |
| Z-02 | Start, vollständige Wertungsrunde, Rangliste, Audio und Kabelanzeige funktionieren ohne Internet, WLAN oder externen Server. |
| Z-03 | Werkzeuge, Effekte, Texte, Schwellenwerte und Audio sind über versionierte Konfiguration und Assets austauschbar. |
| Z-04 | Der Watcher verstärkt das Geschehen, ohne eine zweite Eingabequelle oder einen zweiten Spieler zu erzeugen. |
| Z-05 | Der Aufbau ist vom Standpersonal mit einem definierten Startablauf und Fallback bedienbar. |
| Z-06 | Das Spiel erzeugt Leads und Gesprächsanlässe mit potenziellen Kunden; eine E-Mail-Adresse ermöglicht die Preisbenachrichtigung nach der Messe. |

| Nicht-Ziel (Scope) | Begründung |
|-|-|
| NZ-01 | Keine realistische CNC-/Maschinensimulation. | Die kurze Messeinteraktion priorisiert Verständlichkeit und Reaktionsfreude. |
| NZ-02 | Keine cloudabhängigen Konten, Online-Bestenlisten oder Telemetrie. | Der Messebetrieb muss ohne Internet auskommen und Datenschutzaufwand klein halten. |
| NZ-03 | Kein gleichzeitiges Mehrspieler-Spiel. | Genau eine aktive Rolle verhindert widersprüchliche Touch-Eingaben und vereinfacht den Betrieb. |
| NZ-04 | Keine produktive Asset-Erstellung in diesem Konzept. | Soundzuordnung und Lieferdateien werden konkret festgelegt; fehlende Medien werden nicht als vorhanden ausgegeben. |

## 1.3 Zielgruppen- und Messekontext

Primäre Nutzer sind vorbeigehende Fachbesucher und Begleitpersonen mit unterschiedlichen Vorkenntnissen. Die Interaktion muss sowohl im Vorbeigehen lesbar als auch ohne Erklärung spielbar sein. Sekundäre Nutzer sind Zuschauer am großen Bildschirm und das Standpersonal, das Startbereitschaft, Lautstärke und Störungen steuert.

Der Ablauf beginnt im Attract-Mode: bewegte Werkzeugsilhouetten und eine kurze, rein visuelle Demonstration von Tap und Swipe ziehen Aufmerksamkeit an. Audio startet erst nach bewusster Browserinteraktion; danach begleitet 90er-Arcade-Musik das Spiel. Ein großer Startbereich beginnt die Runde. Eine kurze Vorführung während der ersten Ziele ersetzt eine Textanleitung. Nach dem Ergebnis kehrt das System automatisch zum Attract-Mode zurück.

**Tonalität:** Das ist bewusst ein Fungame und eine Überraschung, kein Produktfreigabeprojekt. Selbstironie, überzeichnete Werkstückstimmen und ein liebevoll-romantischer Blick auf die historische Retro-Bedienoberfläche sollen vermeintlich bessere Zeiten spielerisch neu beleben. Kein Marketing-/Fachworkshop und keine formale Humorfreigabe sind vorgesehen. Technische Funktionsprüfungen und die tatsächlichen gesetzlichen Pflichten bleiben davon getrennt.

**Begriffe:** „Handheld“ bezeichnet in den folgenden Layoutabschnitten die aktive Touchansicht auf demselben Convertible, kein zusätzlich benötigtes Gerät. „Watcher“ bezeichnet eine passive Browseransicht, lokal auf dem Kabelmonitor oder optional auf einem weiteren Gerät.

# 2. Analyse / Ist-Zustand

## 2.1 tl;dr
- Das Repository enthält Dokumentation, Arbeitsvorgaben, ein Anschauungspaket, zehn Roh-MP3-Dateien unter [fx/audio](../../fx/audio) sowie die ersten produktionsfertigen Sounddateien unter `Assets/audio/`; den aktuellen Ausbaustand führt die [Fortschrittsseite](00001-p-vollmer-messe-game.progress.md).
- Die Dokumentationsstruktur war leer; [00000-issues.md](../00000-issues.md), dieses Konzept und die Fortschrittsseite bilden den ersten dokumentierten Arbeitsstand.
- Der bestehende Stack-Favorit bleibt bestehen; geändert wird die Topologie zu Server und aktivem Client auf demselben Windows-Convertible.

## 2.2 Repository-Vorgaben und Lücken

[.github/copilot-instructions.md](../../.github/copilot-instructions.md) verlangt deutsche Dokumentation und Kommentare, transparente Angaben zu fehlenden Punkten, konkrete Empfehlungen für Entscheidungen/Risiken und enge, nachweisbare Validierung. Der Skill [concept-plan](../../.github/skills/concept-plan/SKILL.md) schreibt die Kapitel 0 bis 8, den Index und die Fortschrittsseite vor; [implementation-workflow](../../.github/skills/implementation-workflow/SKILL.md) erklärt diese Fortschrittsseite für die spätere Umsetzung zur verbindlichen Arbeitsliste. Bei C#-Tests gilt [csharp-mstest](../../.github/skills/csharp-mstest/SKILL.md).

Der Ausbaustand der Anwendung steht auf der [Fortschrittsseite](00001-p-vollmer-messe-game.progress.md). Das Anschauungspaket, die Rohdateien `yell01.mp3` bis `yell10.mp3` und die produktionsfertigen Sounddateien unter `Assets/audio/` sind Medienbestand, kein Anwendungsnachweis. Die globale [.gitignore](../../.gitignore) bleibt unverändert. Der Nutzer bestätigt ausschließlich royaltyfree verwendbare oder eigene Kreationen/Kompositionen und übernimmt Pitching/Bearbeitung selbst. Damit ist OP-03 geklärt; selbst gewählte Ergänzungen müssen ebenfalls frei für den vorgesehenen Einsatz verwendbar sein.

Für die Werkzeugbilder (Bohrer, PKD-Fräser, Sägeblätter) fehlte bis 2026-09-27 eine konkrete Produktions- und Trefferzonenvorgabe: Es war nicht festgelegt, wie ein Bild aufgebaut sein muss und welche Zone bei Tap oder Swipe Erfolg oder Misserfolg auslöst. Diese Lücke ist jetzt geschlossen: Kap. 3.3.1 regelt verbindlich Erfolg/Misserfolg je Geste, Kap. 3.4.5 die verbindlichen Zonen-, Größen- und Zustandsregeln. Am 2026-09-28 kam eine dritte Lücke hinzu: Bildmengen, Zustandsbilder, Störgegenstände und die Technik der 2D-Tiefe waren nicht bestimmt. **Dieses Hauptkonzept führt daher keine Bildlieferliste mehr.** Die Bildlieferliste mit Anzahl, IDs, Varianten, Zuständen (`dull`, `in_progress`, `sharp`, `failed`), Requisiten, Störgegenständen, Ablagestruktur und dem Tiefenverfahren steht in [00002](00002-c-vollmer-assetlieferung.concept.md); die Manifestgenerierung und die Autoren-Hilfstools regelt [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md).

# 3. Lösungskonzept / Soll-Zustand

## 3.1 tl;dr
- Der aktive Client bestätigt Berührungen lokal in höchstens $50\text{ ms}$; der Server bestätigt Wertung und Rundenstatus verbindlich.
- Laserziele verlangen einen Tap, Schleifziele einen gerichteten Swipe; Form, Farbe, Bewegung und Effekt unterscheiden die Bearbeitungsarten sichtbar.
- Der Spielkern bleibt datengetrieben: Tooldefinitionen, Spawnmuster, Modi, Punkte, Texte, Audio und Layoutvarianten liegen außerhalb der Kernlogik.

## 3.2 Nicht-Ziele

| Nicht-Ziel (Implementierung) | Begründung |
|-|-|
| NZ-05 | Blazor rendert nicht den Game-Loop. | DOM-basierte UI-Updates sind für Partikel, Canvas und hochfrequente Touch-Animationen nicht die passende Verantwortung. |
| NZ-06 | Kein Peer-to-Peer-Fallback für wertungsrelevante Runden. | Ohne zentrale Autorität sind Rollen, Timer und Punktestand nicht belastbar lösbar. |
| NZ-07 | Keine frei editierbaren Laufzeitskripte am Messestand. | Änderungen sollen sicher über geprüfte Konfigurationen erfolgen. |

## 3.3 Spielablauf und Kernmechaniken

1. **Standby/Attract:** Der Watcher zeigt eine großformatige Endlosszene; das Handheld zeigt einen eindeutigen Startbereich. Nach Inaktivität startet die Szene erneut.
2. **Start und Onboarding:** `F2 LASER` oder `F3 SCHEIBE` bestimmt vor dem Start verbindlich den Rundenmodus; genau einer der beiden Modi ist aktiv. `F1 START` reserviert die aktive Rolle und startet die gewaehlte Runde. Die ersten zwei Ziele demonstrieren je nach Modus Tap-Laser oder Swipe-Schleifen mit verlangsamter Bewegung und klarer Geste.
3. **Hauptphase:** Stumpfe Werkzeuge fliegen einmalig über die gesamte Leinwand: zufällig links nach rechts, rechts nach links, oben nach unten oder unten nach oben. Start und Ende liegen unter Berücksichtigung der vollständigen 16:9-Drehhülle außerhalb des sichtbaren Bildes. Variierende kubische Bézier-Bahnen werden serverseitig in 6–9 Sekunden durchlaufen, ohne Rücklauf oder Abstandsblockierung. Danach wird das Werkzeug ohne Punkteabzug entfernt. Überschneidungen sind erlaubt; bei gleichem Kontaktzeitpunkt wird das sichtbare vordere Werkzeug gewertet, innerhalb desselben Werkzeugs bleibt der Schaftvorrang erhalten. Zusätzlich werden je Werkzeug links/rechts und eine konstante Drehgeschwindigkeit von 12–30°/s zufällig gewählt. Schwierigkeit steigt über Bahnvariation, Spawnrate, Geschwindigkeit, Mischungen und kürzere Reaktionsfenster, nicht über versteckte Regeln. Trefferzonen und verlangte Säge-Swipe-Richtung bleiben an die bestätigte Ausrichtung gebunden, die Mindestlänge ist winkelunabhängig.
4. **Feedback:** Jede abgeschlossene erfolgreiche oder nicht erfolgreiche Spielaktion erhält sichtbares Feedback und eine Audioantwort gemäß Kap. 3.4. Falsches Schärfen und Laser-Treffer auf den Schaft kosten Punkte; die Runde läuft unabhängig vom Punktestand weiter. Es gibt weder Leben noch ein vorzeitiges Game-over. Ein falsch bearbeitetes Werkzeug wechselt sichtbar in den Zustand `failed` (zerstört), ist danach nicht mehr auswertbar und verschwindet beschleunigt (Kap. 3.3.1, [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.5).
5. **Ende:** Der Server beendet nach standardmäßig $60\text{ s}$ die Wertung, beide Ansichten zeigen Ergebnis und die Rangliste des gespielten Modus. Dauer und Punktewerte sind konfigurierbar. Das Formular erfasst Name und E-Mail für Leads und die Preisbenachrichtigung nach der Messe. Wer keine Kontaktdaten angeben möchte, trägt einen Fantasienamen ohne E-Mail ein und kann seinen Highscore trotzdem speichern. Ohne erreichbare E-Mail-Adresse gibt es keinen Preis; die Teilnahme am Spiel und die Ranglistenanzeige bleiben möglich. `Ohne Eintrag weiter` verwirft das Formular. Der konfigurierbare Datenschutzhinweis erläutert die Zwecke vor dem Speichern (Kap. 3.8).
6. **Rückkehr:** Nach konfigurierbaren $10$ bis $20\text{ s}$ ohne Eingabe oder nach Abschluss der Namenseingabe wird die Rolle freigegeben und der Attract-Mode geladen.

**Rollenwechsel:** Die reguläre Spieleransicht erhält kein frei sichtbares Rollenmenü. Am Watcher beziehungsweise Betreiber-Host öffnet ein unauffälliger, dokumentierter Langdruck einen Betreiber-Dialog. Der Dialog zeigt den verbundenen aktiven Client und bietet die Aktionen `Rolle freigeben` sowie `Runde abbrechen und Rollen freigeben`. Beide Aktionen verlangen einen bewusst ausgeführten Slide-to-confirm-Schalter. Während einer laufenden Runde ist nur die zweite Aktion verfügbar, damit eine Wertung nicht still an eine andere Person übergeht. Nach einer Freigabe oder einem Abbruch kehren alle Clients zum Attract-Mode zurück; der nächste Handheld-Tap kann die aktive Rolle übernehmen. Der Server führt die bereits definierte atomare Übergabe mit erhöhter `RoleVersion` aus.

**Laser-Modus:** Bohrer und Fräser unterschiedlicher Größe, teilweise mit PKD (polykristallinem Diamant), besitzen jeweils einen sichtbaren Schaft. Das Werkzeugmanifest enthält getrennte Polygone für bearbeitbare Schneiden und Schaft; Größenvarianten skalieren Bild und Zonen gemeinsam. Ein Tap auf eine Schneide ist erfolgreich, ein Tap auf den Schaft ist eine Fehlbearbeitung mit Punktabzug. An einer gemeinsamen Zonengrenze hat der Schaft Vorrang; es gibt genau ein Ergebnis pro Geste. Cyanfarbene Zielmarkierungen und ein punktförmiger Lichtimpuls machen die gültige Zone lesbar. Die hochgepitchten Stimmen geben den Werkstücken den gewünschten comicartigen, an „Worms“ erinnernden Charakter; Originalspiel-Aufnahmen sind damit nicht vorausgesetzt.

**Schleif-Modus:** Sägeblätter sind die primären Werkstücke und erhalten die noch zu liefernden tiefer gepitchten Stimmen. Ein Swipe entlang der markierten Schneide löst eine virtuelle Schleifscheibe und gerichtete Funken aus. Ein falscher Winkel, eine zu kurze Geste oder eine Bearbeitung des Blattkörpers zählt als falsches Schärfen. Richtung und Mindeststrecke sind sichtbar und tolerant konfigurierbar; weitere Werkzeugtypen benötigen erst eigene Zonen und Soundzuordnungen.

**Bevorzugte Startwerte für die Punktebalance:** `roundDurationSeconds: 60` (zunächst validierter Bereich 30 bis 90), `correctScore: 10`, `wrongSharpeningPenalty: -5`, `shaftHitPenalty: -10`, `missPenalty: -5`. Negative Gesamtstände sind zulässig. Die Combo ist vorerst nur Anzeige, kein Multiplikator. Ein unbearbeitet vorbeiziehendes Werkzeug verursacht keinen Abzug und keinen Interaktionssound. Diese Werte sind ein konkreter konfigurierbarer Entwurf, keine fachliche Freigabeanforderung.

Eine Spielaktion ist genau eine im Canvas begonnene und abgeschlossene Tap-/Swipe-Geste, nicht jeder Pointer-Move. Pro Geste wird höchstens ein Werkzeug ausgewertet: zuerst die früheste getroffene Zone entlang des Pfads, bei Gleichstand die vorderste sichtbare Zone. Der falsche Gestentyp auf einem Werkzeug zählt als `wrong_sharpening`; im Laser-Modus hat ein erkannter Schaftkontakt Vorrang. Eine Geste ohne Werkzeugkontakt zählt als `miss`. Bereits vollständig geschärfte Werkzeuge werden aus den Trefferzonen entfernt. Abgebrochene Pointer-Gesten, gesperrte Bedienelemente und Eingaben nach Rundenende sind keine neuen wertungsrelevanten Aktionen. Ein Rundentimer wird durch Overlays nicht angehalten.

### 3.3.1 Verbindliche Entscheidung je Geste: Erfolg, Misserfolg, Sound und Effekt

Diese Regeln sind die einzige Autorität für die Zuordnung „Erfolg“ oder „Misserfolg“. Client und Server werten dieselben normalisierten Trefferzonen und Toleranzen aus der versionierten Konfiguration aus; der Client nutzt sie nur für die lokale visuelle Vorhersage, der Server für die verbindliche Wertung. Hörbar und wertungsrelevant wird ein Ereignis ausschließlich über das bestätigte `ActionResolved` (Kap. 3.4.3).

**Begriffe:** Ein *Werkzeugkontakt* liegt vor, wenn der Tap-Punkt oder der Swipe-Pfad die Werkzeugsilhouette innerhalb der Toleranz `contactToleranceNormalized` schneidet. Die *bearbeitbare Schneide* ist die Zone `cut`, der *Schaft* beziehungsweise *Blattkörper* die Zone `shaft`. Beide Zonen sind normalisierte Polygone im Manifest (Kap. 3.4.5).

| Modus | Ausgeführte Geste | Ausgewerteter Kontakt | Ergebnis `Outcome` | Punkte | Audiopool (Kap. 3.4.1) | Sichtbarer Effekt |
|-|-|-|-|-:|-|-|
| LASER | Tap | Tap-Punkt in Zone `cut` | `success` | +10 | `laser_success` | cyanfarbener Fokuspunkt und Lichtimpuls am Kontaktpunkt, grüne HUD-Bestätigung |
| LASER | Tap | Tap-Punkt in Zone `shaft`; an der gemeinsamen Grenze hat `shaft` Vorrang | `shaft_hit` | -10 | `laser_fail` | kurzer roter Impuls am Schaft, Schaftblitz, rotes HUD-Kreuz |
| LASER | Swipe | Pfad berührt das Werkzeug innerhalb der Toleranz | `wrong_sharpening` | -5 | `laser_fail` | roter Wischimpuls am Werkzeug, rotes HUD-Kreuz |
| LASER | Tap oder Swipe | kein Werkzeugkontakt innerhalb der Toleranz | `miss` | -5 | keine Stimme; nur `laser_effect` | kurzer roter Ring am Gestenpunkt, kein Werkzeugeffekt |
| SCHEIBE | Swipe | Geste beginnt und endet außerhalb der Werkzeugsilhouette, streift Zone `cut` und berührt Zone `shaft` nicht | `success` | +10 | `saw_success` | Schleifscheibe läuft an, gerichtete Funken entlang der Schneide, grüne HUD-Bestätigung |
| SCHEIBE | Swipe | Start oder Ende innerhalb der Werkzeugsilhouette oder Pfad berührt Zone `shaft`/Blattkörper | `wrong_sharpening` | -5 | `saw_fail` | kurzer Funkenabriss am Blattkörper, rotes HUD-Kreuz; Werkzeug wird defekt |
| SCHEIBE | Tap | Werkzeugkontakt innerhalb der Toleranz | `wrong_sharpening` | -5 | `saw_fail` | roter Stempel am Werkzeug, rotes HUD-Kreuz |
| SCHEIBE | Swipe | Pfad berührt nur Zone `shaft`/Blattkörper | `wrong_sharpening` | -5 | `saw_fail` | roter Wischimpuls am Blattkörper, rotes HUD-Kreuz |
| SCHEIBE | Tap oder Swipe | kein Werkzeugkontakt innerhalb der Toleranz | `miss` | -5 | keine Stimme; nur `grind_effect` | kurzer roter Ring am Gestenpunkt, kein Werkzeugeffekt |

**Auswertungsregeln (verbindlich):**
- Pro Geste wird höchstens ein Werkzeug ausgewertet: die früheste entlang des Pfads getroffene Trefferzone, bei Gleichstand die vorderste sichtbare Zone.
- Bei gemeinsamer Grenze der Zonen `cut` und `shaft` hat `shaft` Vorrang; es gibt genau ein Ergebnis pro Geste.
- Ein falscher Gestentyp auf einem Werkzeug zählt als `wrong_sharpening`. Ein erkannter Schaftkontakt hat im Laser-Modus Vorrang vor `wrong_sharpening`, wenn eine Geste beide Bedingungen erfüllen würde.
- Eine Geste ohne Werkzeugkontakt zählt als `miss`. Ein unbearbeitet vorbeiziehendes Werkzeug löst weder Ergebnis noch Sound aus.
- Bereits vollständig geschärfte Werkzeuge werden aus den Trefferzonen entfernt und sind bis zum Despawn nicht erneut auswertbar.
- Abgebrochene Pointer-Gesten, Eingaben auf gesperrten Bedienelementen und Eingaben nach Rundenende sind keine wertungsrelevanten Aktionen.
- Ein Werkzeug wechselt genau einmal den Zustand: von `dull` zu `sharp` bei bestätigtem `success` oder zu `failed` bei bestätigtem `wrong_sharpening` oder `shaft_hit`. `miss` verändert den Zustand nicht.
- `sharp` und `failed` sind bis zum Despawn nicht erneut auswertbar: Ein Kontakt löst kein Ergebnis, keine Punkte und keinen Sound aus. `failed`-Werkzeuge verschwinden beschleunigt (`failedDespawnMs`, Startwert 600 ms nach dem auslösenden Ereignis).

**Konkreter Startwert der Kontaktoleranz (konfigurierbar, validiert):** `contactToleranceNormalized: 0.02`. Für den Scheibenmodus sind weder Wischrichtung noch Mindestlänge relevant; Start und Ende müssen außerhalb der Werkzeugsilhouette liegen, die Geste muss `cut` streifen und darf `shaft` nicht berühren.

**Sichtbarer Erfolg und Misserfolg:** `success` löst den modusbezogenen Success-Pool aus; `wrong_sharpening`, `shaft_hit` und `miss` lösen den modusbezogenen Fail-Pool aus. Zusätzlich startet jede bestätigte Geste das modusbezogene Effektsignal (`laser_effect` im Laser-Modus, `grind_effect` im Schleif-Modus; Kap. 3.4.2). Ablauf, Kanalgrenzen und Ducking stehen in Kap. 3.4.3, die Zuordnung der Zonen zu den konkreten Dateien in Kap. 3.4.1 und 3.4.2.

## 3.4 Grafik-, Asset- und Soundkonzept

**Werkzeugcharaktere (Nutzerentscheidung vom 2026-10-13):** Auf dem Werkzeugkörper werden Comicgesichter gezeigt, ohne die Schneidenmarkierung zu verdecken oder Trefferzonen zu ändern. Die Mimik folgt `dull` (müde), `in_progress` (konzentriert), `sharp` (zufrieden) und `failed` (erschrocken/defekt). Basisbild, Körpermimik und Schneiden-Overlay ergeben gemeinsam die Zustandssprites; vier vollständige Werkzeugbilder sind nicht erforderlich. Die Wiedergabe der bereits gelieferten Werkstückstimmen und Laser-/Schleifeffekte wird als ausdrücklich freigegebener Teilumfang aus AP-05 vorgezogen; vollständiger Musikmischer, UI/Jingles, Datenschutz und Ranglisten bleiben davon getrennt.

Die visuelle Richtung verbindet den orangefarbenen Showroom-Boden und die feste Showroom-Kulisse (Kap. 3.5) mit der historischen gelben Arbeitsfläche als Shell-Akzent, dem anthrazitfarbenen Gehäuse und den grünen/blauen Retro-Bedienelementen zu einer comicartigen Produktionswelt. Der Wiedererkennungswert und die liebevolle Überzeichnung sind ausdrücklich erwünscht. Werkzeuge benötigen klar lesbare Silhouetten; die Zustände `dull`, `in_progress` und `sharp` werden als Overlay über die Trefferzone `cut` gezeichnet und nicht als eigene Bilddatei geliefert (Kap. 3.4.5). Fotorealistische Details sind nur dort sinnvoll, wo sie die Form nicht verschleiern.

**Farbe und Schrift:** Das bereitgestellte VWC-Design-System ist verbindliche Leitplanke für Bedienhülle, HUD und Overlays, keine Beschränkung für Spielwelt, Werkzeuge, Effekte oder Humor. Orange ist der wiederkehrende Markenakzent: `vwc.base.color.orange.600` (`#ff8f00`) für Start, Fokus, Fortschritt und bestätigte Hauptaktionen; `orange.400` (`#e87900`) für aktive/gedrückte Zustände; `orange.800` (`#ffe9cc`) für zurückhaltende Hervorhebung. Anthrazit nutzt `grey.100` (`#3b3b3b`) und `grey.200` (`#505050`), die historische Arbeitsfläche `yellow.600` (`#ffed00`) bleibt als Shell- und Weltakzent erhalten. Die Spielbühne selbst ist jetzt der Showroom aus Kap. 3.5: ein orangefarbener Boden als Weltfarbe, kein Bedienflächen-Token. Cyan `blue.600` (`#00bbfc`) bleibt dem Laser vorbehalten; Grün `green.600` (`#94e200`) kennzeichnet korrektes Spiel-Feedback und Rot `red.600` (`#fe0e26`) Fehler oder Abzug. Damit bleibt die Hülle wiedererkennbar, während Assets, Parallaxe und Effekte ihre eigene comicartige Farbdramaturgie verwenden dürfen; Orange ist kein pauschaler Ersatz für das bestehende Gelb. Der Bodenorangewert der Bühne liegt bewusst unter Sättigung und Helligkeit von `orange.600` (Konfigurationswert, kein neuer Design-Token), damit Zielsilhouetten mit dunkler Kontur sowie Cyan-, Grün- und Rot-Feedback darauf lesbar bleiben.

| Bereich | Design-Token | Einsatzregel |
|-|-|-|
| Primäre Bedienaktion und Fokus | `orange.600`, `orange.400` | Start, ausgewählter Modus, Tastatur-/Touchfokus und bestätigte Hauptaktion; nie als Laser- oder Fehlerfarbe verwenden. |
| Retro-Gehäuse und neutrale Fläche | `grey.100`, `grey.200`, `grey.700` | Gehäuse, Ränder, Trennlinien und deaktivierte Bedienelemente. |
| Arbeitsfläche und Spielwelt | Showroom-Bühnenorange (abgesenkter Konfigurationswert); `yellow.600`; freie Produktionspalette | Die Spielbühne ist die Showroom-Kulisse aus Kap. 3.5/3.6 mit orangefarbenem Boden; `yellow.600` bleibt Shell- und Weltakzent (Lichtband, Maschinenmarkierung, HUD-Binnenfläche) und ist nicht mehr die Grundfarbe des Canvas. Die Spielwelt darf zusätzliche, kontraststarke Farben nutzen, solange Ziele und Gesten lesbar bleiben. |
| Fachliches Feedback | `blue.600`, `green.600`, `red.600` | Laser, Erfolg und Fehler bleiben semantisch eindeutig und konkurrieren nicht mit dem orangefarbenen Markenakzent. |

Für HUD, Bedienhülle und Formulare wird `Source Sans Pro` aus dem Design-System bevorzugt lokal gebündelt eingesetzt. Eine eigenständige, frei verwendbare Akzentschrift für Attract-Mode oder Werkstückkomik ist zulässig, sofern sie Lesbarkeit und die Bedienhülle nicht verdrängt. Keine kostenpflichtige Corporate-Schrift und kein fremdes restriktiv lizenziertes Gestaltungspaket werden vorausgesetzt. Erforderliche Lizenzhinweise bleiben im Paket; eine zusätzliche Designfreigabe ist nicht vorgesehen.

Das HUD bleibt am Handheld kompakt: oben Zeit und Punkte, darunter Combo und Modusindikator. Der Watcher nutzt dieselben Ereignisse, zeigt aber großformatige Werkzeugbewegungen, den aktuellen Spielerstatus, Punkte, Combo und optional die Tagesrangliste. Der Watcher enthält keine Eingabecontrols.

Empfohlene Laufzeitstruktur: `Assets/images`, `Assets/animations`, `Assets/audio`, `Assets/fonts`, `Assets/templates` und `Assets/config`; die vollständige Ablage- und Solution-Struktur steht in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.3. Jedes Asset besitzt eine monotone, pro Kategorie eindeutige Manifest-ID, etwa `tool_0001` oder `sfx_0001`; eine entfernte ID bleibt reserviert. Audiodateinamen beschreiben die Aktion nach dem Schema `<aktion>_<nummer>.<format>`, beispielsweise `cheer_high_01.wav`. Produktionsfertige Sounds werden direkt in ihre Zielordner unter `Assets/audio/{ui,laser,grind,ambient,music}` geliefert und nach der Lieferung nicht mehr umbenannt; [fx/audio](../../fx/audio) bleibt Eingangsablage für Rohmaterial wie die `yell`-Originaldateien. Die Runtime verwendet die Manifestzuordnung, nicht eine aus Dateinamen abgeleitete Spiellogik. Weitere Varianten benötigen keinen Codeumbau.

Die fachliche Bedeutung liegt ausschließlich im versionierten Manifest, beispielsweise `tool_0001` mit `toolType: "drill"`, `interaction: "tap"`, `sizeClass: "small"`, den normalisierten Trefferzonen `hitZones` (Rollen `cut`/`shaft`), `pivot` und Audiogruppen; die verbindliche Vorlage steht in Kap. 3.4.5. Das Manifest wird **generiert** und nicht von Hand gepflegt; Aufbau, Beispiel, Autorendateien und Werkzeugkette stehen in [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md). Eine Assetvorlage pro Kategorie dokumentiert Pflichtfelder, Formate, Pivot, Auflösung, Varianten, Performancegrenzen und Fallbacks. Ein Manifest-Validator prüft vor jeder Runde IDs, referenzierte Dateien und Vorlagenkonformität; bei Fehlern bleibt das letzte gültige Manifest aktiv. Fehlt auch dieses, startet nur ein ausdrücklich gekennzeichneter Demo-Modus. PNG/WebP eignet sich für Rastergrafiken, JSON-Atlanten für Sprites. Die gelieferten MP3- und WAV-Dateien werden direkt unterstützt und vor Spielbeginn dekodiert; OGG bleibt optional und ist nicht erforderlich. Kein CDN und kein Streaming sind erforderlich.

**Dateiübersicht:** Die Bilddateien mit Format, Auflösung, Vorlage und Varianten stehen vollständig in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4; die Audiodateien stehen in Kap. 3.4.4 dieses Dokuments. Eine dritte, eigene Übersicht wird bewusst nicht geführt: Jede Angabe existiert an genau einer Stelle.

Audio ist in `Assets/audio/{ui,laser,grind,ambient,music}` organisiert. Ein Audioprofil legt getrennte Lautstärken für Musik, kurze Feedbacksignale und Werkstückstimmen fest. Die folgende Zuordnung ist die konkrete Liefer- und Implementierungsvorgabe; sie ist noch keine vorhandene Laufzeitimplementierung. Audio darf eine sichtbare Aktion nie blockieren. Herkunft und Nutzungsrechte werden schlank im Manifest dokumentiert, ohne zusätzlichen kreativen Freigabeprozess.

### 3.4.1 Konkrete Zufallszuordnung der Werkstückstimmen

**Bestand:** Die produktionsfertigen Werkstückstimmen sind seit 2026-10-11 geliefert und liegen als WAV-Dateien unter `Assets/audio/laser/` (Laser-Modus) und `Assets/audio/grind/` (Schleif-Modus); die frühere Übernahmeplanung aus den `yell`-Rohdateien ist damit erledigt. Die ursprünglichen Rohdateien `yell01.mp3` bis `yell10.mp3` bleiben unverändert unter [fx/audio](../../fx/audio) und gehören nicht zur Laufzeit. Die Dateinamen sind endgültig und werden nicht umbenannt; ein noch ausstehender Klang-/Pegel-Hörcheck kann höchstens die Manifestzuordnung ändern. Der Nutzer pitcht und bearbeitet selbst und verwendet ausschließlich royaltyfree verwendbare Quellen oder eigene Kreationen/Kompositionen (OP-03 geklärt).

| Modus / Audiopool | Exakte Bedingung | Punkte (Startwert) | Konkrete Startdateien, gleiche Gewichtung | Klang / Status |
|-|-|-|-|-|
| LASER / `laser_success` | `success`: Tap-Punkt in Zone `cut` von Bohrer oder Fräser, mit oder ohne PKD | +10 | `cheer_high_01.wav`, `cheer_high_02.wav`, `cheer_high_03.wav`, `cheer_high_04.wav` | hochgepitchter Jubel; vier Dateien geliefert |
| LASER / `laser_fail` | `wrong_sharpening` oder `shaft_hit` (tatsächliche Fehlbearbeitung oder Schafttreffer) | -5; `shaft_hit` -10 | `yell_high_01.wav`, `yell_high_02.wav`, `yell_high_03.wav`, `yell_high_04.wav`, `yell_high_05.wav` | hochgepitchter Protest; fünf Dateien geliefert |
| SCHEIBE / `saw_success` | `success`: Swipe kreuzt Zone `cut` mit zulässiger Richtung und Mindeststrecke | +10 | `cheer_low_01.wav`, `cheer_low_02.wav`, `cheer_low_03.wav`, `cheer_low_04.wav` | tiefer gepitchter zufriedener Ausruf; vier Dateien geliefert |
| SCHEIBE / `saw_fail` | `wrong_sharpening` bei Werkzeugkontakt (falsche Richtung, zu kurzer Swipe, Tap oder Blattkörper) | -5 | `yell_low_01.wav`, `yell_low_02.wav`, `yell_low_03.wav`, `yell_low_04.wav`, `yell_low_05.wav` | tiefer gepitchter Protest; fünf Dateien geliefert |

**Poolgröße:** Pro Aktion sind 3–6 Dateien das Produktionsziel, keine technische Mindest-/Höchstgrenze. Alle tatsächlich konfigurierten Varianten nehmen an der Zufallsauswahl teil; auch ein oder mehr als sechs Clips funktionieren ohne Codeänderung. Die Startliste verwendet vier bis fünf Varianten je Stimmenpool und vier bis sechs je Effektpool. Weitere Dateien folgen dem gleichen Namen mit `_04`, `_05`, `_06` usw. Fehlbearbeitung und Schaft bleiben für Punkte und Bildfeedback unterscheidbar, teilen aber den jeweiligen `fail`-Stimmenpool. Fehlklicks ohne Werkzeugkontakt verwenden nur das Effektsignal, keine Stimme.

Jeder Pool verwendet einen zufällig gemischten Beutel: Alle vorhandenen Varianten werden einmal gezogen, danach neu gemischt; bei mehr als einer Variante keine unmittelbare Wiederholung an der Beutelgrenze. Ein Ein-Datei-Pool spielt seine einzige Datei; ein leerer Pool nutzt den definierten Audio-Fallback. Gemeinsam verwendete Pools teilen den Beutel. Ziehung nur beim tatsächlichen Soundstart, keine Häufung durch verworfene Ereignisse. Keine zusätzliche Laufzeit-Pitchänderung der bereits bearbeiteten Lieferdateien. Empfohlene Sprachclips: 0,3 bis 1,2 Sekunden, maximal 1,8 Sekunden, ohne lange führende Stille, ohne Musikbett, mit einheitlicher wahrgenommener Lautheit und ohne Clipping. Längere Stimmdateien benötigen einen konfigurierten, hörgeprüften Ausschnitt.

### 3.4.2 Feedbacksignale, Bedienaktionen und Musik

**Fehlklick ins Leere (Nutzerentscheidung vom 2026-10-13):** Bei `miss` wird keine Werkstückstimme und damit kein Schrei gestartet. Nur das modusbezogene Effektsignal bleibt erhalten; Punkteabzug und sichtbare Rückmeldung ändern sich nicht. Noch vorgemerkte Stimmen werden verworfen, damit kein verzögerter Schrei auf den Fehlklick folgt. Bereits laufende Stimmen stammen gegebenenfalls von einer vorherigen tatsächlichen Bearbeitung.

Die modusbezogenen Effektsignale `laser_*` und `grind_*` sind geliefert und liegen unter `Assets/audio/laser/` beziehungsweise `Assets/audio/grind/`. Hintergrundmusik ist seit 2026-10-13 als `bgm.mp3` geliefert und bytegleich unter `Assets/audio/music/` übernommen. Das Intro 0:00–0:15 wird übersprungen; beim bestätigten Rundenstart wird ab 0:15 gespielt und nur der Abschnitt 0:15–2:18 wiederholt. UI-/Jingledateien unter `Assets/audio/ui/` fehlen weiterhin. Aktionspools verwenden 3–6 Varianten als Ziel; Hintergrundmusik benötigt keine Varianten.

| Aktion / Ereignis | Exakte Datei(en) | Auslösung und Klangauftrag |
|-|-|-|
| Jede bestätigte Spielgeste im Laser-Modus | `laser_01.wav`, `laser_02.wav`, `laser_03.wav`, `laser_04.wav`, `laser_05.wav`, `laser_06.wav` | zufälliges Laser-Effektsignal im Kurzfeedbackkanal, unabhängig vom Ergebnis; Erfolg/Misserfolg trägt die Werkstückstimme; geliefert |
| Jede bestätigte Spielgeste im Schleif-Modus | `grind_01.wav`, `grind_02.wav`, `grind_03.wav`, `grind_04.wav` | zufälliges Schleif-Effektsignal im Kurzfeedbackkanal, unabhängig vom Ergebnis; Erfolg/Misserfolg trägt die Werkstückstimme; geliefert |
| Gültige UI-Aktion | `ui_confirm_01.mp3`, `ui_confirm_02.mp3`, `ui_confirm_03.mp3` | zufälliger Retro-Klick, höchstens 120 ms; Moduswahl, F4/F5, Datenschutz öffnen/schließen, Speichern, `Ohne Eintrag weiter`, bestätigte Betreiberaktion |
| Abgelehnte UI-Aktion | `feedback_fail_01.wav`, `feedback_fail_02.wav`, `feedback_fail_03.wav` | Fehlerpool bei ungültiger Formulareingabe oder nicht zulässigem Befehl; keine Punktewirkung |
| `F1 START` akzeptiert | `round_start_01.mp3`, `round_start_02.mp3`, `round_start_03.mp3` | zufälliger Start-Jingle, höchstens 800 ms; ersetzt den UI-Klick |
| Reguläres Rundenende | `round_end_01.mp3`, `round_end_02.mp3`, `round_end_03.mp3` | zufälliger Abschluss-Jingle, höchstens 1.200 ms; kein zusätzlicher Combo-/Highscore-Jingle |
| Jede akzeptierte rechte Ereignistaste | `ui_confirm_01.mp3`, `ui_confirm_02.mp3`, `ui_confirm_03.mp3` | gemeinsamer UI-Pool; kein zusätzliches Sprach-/Gag-Audio und keine Punktewirkung |
| `F6 TON` | `ui_confirm_01.mp3`, `ui_confirm_02.mp3`, `ui_confirm_03.mp3` | gemeinsamer UI-Pool beim Wechsel in laut/leise im neuen Pegel; Wechsel in stumm bleibt lautlos |
| Hintergrundmusik | `bgm.mp3` | gelieferte Musik; Start/Loop ab 0:15, Intro übersprungen, nur in aktiver Runde; F6 steuert den gemeinsamen Pegel |

Scrollen im Overlay, Tippen in Textfelder, Pointer-Moves, automatische Zielbewegungen und Combo-Anzeigen lösen keine zusätzlichen Sounds aus. Gemeint ist eine abgeschlossene Spielgeste oder ein ausgelöster UI-Befehl, nicht jedes technische Eingabeereignis. F6 stumm, die Systemstummschaltung und ein defekter Audiopfad sind explizite Ausnahmen vom hörbaren Feedback; sichtbare Rückmeldung bleibt immer erhalten.

### 3.4.3 Mischer, Ducking und Begrenzung

**Favorit:** Ein gemeinsamer Web-Audio-Mischer des aktiven Clients mit Musikbus, einem Kurzfeedbackkanal und genau einem Sprach-/Jinglekanal. Shell und Phaser verwenden denselben Mixer; optionale Watcher bleiben stumm. Damit gibt es höchstens zwei Effektsounds plus einen Musikstream gleichzeitig, kein zusätzliches Ambientbett und kein Audioecho von einer zweiten Anzeige.

- Jede bestätigte Spielgeste startet das modusbezogene Effektsignal aus `laser_effect` beziehungsweise `grind_effect`. Bei Überlappung beendet ein 3-ms-Fade das vorige Kurzsignal und startet das neue; keine Warteschlange. UI-Klicks verwenden denselben Kurzfeedbackkanal, niemals einen zusätzlichen Kanal. Extrem schnelle Eingaben können akustisch verschmelzen, erzeugen aber keine anwachsende Soundmenge.
- Werkstückstimmen starten frühestens 180 ms nach dem letzten Sprachstart. Während dieser Sperre bleibt nur das jeweils jüngste Ereignis vorgemerkt; es verfällt nach 200 ms und wird bei Rundenende entfernt. Das Effektsignal bestätigt auch die Aktionen, deren Stimme entfällt. Neue zulässige Stimmen ersetzen die laufende Stimme mit 15-ms-Fade-out und anschließendem Start, niemals mit überlappendem Crossfade. Keine wachsende Queue und keine nachträgliche Stimmenkaskade.
- Start-/End-Jingles haben Vorrang vor Werkstückstimmen, ersetzen sie und löschen vorgemerkte Stimmen. Das Endereignis ist einmalig; bei gleichzeitigem Treffer und Rundenende bleibt das Effektsignal des Treffers hörbar, der End-Jingle übernimmt den Sprachkanal. Dekorative Ereignisse dürfen Wertungsfeedback nicht verdrängen und bleiben während einer Spielgeste akustisch nachrangig.
- Auf Nutzerfeedback wird Musik bei Effekten sanft über 80 ms um 3 dB abgesenkt. Nach Ende des letzten Effekts folgen 120 ms Haltezeit und 250 ms Rückkehr zum Normalpegel. Neue Effekte verlängern die Absenkung, addieren aber keine weiteren -3 dB. F6 und Masterpegel multiplizieren den gesamten Mix; Ducking darf Stummschaltung niemals aufheben.
- Angepasstes Startprofil: Musik -16 dB, Werkstückstimmen/Jingles unverändert -6 dB, gelieferte Kurzfeedback-Effekte etwa -14 dB (Gain 0,2), Master-Limiter als Clippingschutz. Die gegenwärtigen Startwerte sind im Code verankert; vollständige validierte Mischer-/Ausschnittkonfiguration und Hörfreigabe bleiben Teil des offenen AP-05-Umfangs.
- Der erste bewusste Start-Tap entsperrt den AudioContext. Vorher ist Attract still; danach läuft der Loop in der Runde und optional mit weiteren -6 dB im Attract-/Ergebniszustand. Vorladen und Dekodieren geschehen vor Rundenstart. Ein Browser-Autoplayfehler wird im Betreiberstatus sichtbar.
- Fachliche Sounds werden erst durch das bestätigte `ActionResolved` ausgelöst, lokal vorhergesagte Effekte bleiben rein visuell. `EventId` verhindert doppelte Wiedergabe; Snapshot/Replay und Watcher-Reconnect spielen keine historischen Sounds ab. Am lokalen Host beträgt das Ziel für bestätigte Audioantworten höchstens 100 ms p95 vom Gestenende bis zum Audio-Start.
- Fällt eine Sprachdatei aus, bleibt das modusbezogene Effektsignal. Fehlt auch dieses, erzeugt Web Audio einen kurzen konfigurierten Ersatzton; dieser ist ausdrücklich kein Produktionsasset. Ohne Audiogerät bleibt der Ablauf sichtbar spielbar und meldet den Fehler dem Betreiber. Eine fehlende Musikdatei führt zu Stille auf dem Musikbus, nicht zum Abbruch der Runde.

### 3.4.4 Konkrete Lieferliste

Die Anzahl ist der konkrete Startvorschlag; 3–6 Varianten pro Aktion sind gewünscht, andere Poolgrößen ändern die Technik nicht. Alle Sounds kommen ausschließlich aus dem aktiven Convertible-Client über dessen gewählten Audioausgang; lokale und entfernte Watcher bleiben stumm.

| Lieferung / Aktion | Exakte Startdateinamen | Anzahl | Wo und wann wird abgespielt? | Status |
|-|-|-:|-|-|
| Laser / Success (Stimme) | `cheer_high_01.wav`, `cheer_high_02.wav`, `cheer_high_03.wav`, `cheer_high_04.wav` | 4 | Im Laser-Spiel nach bestätigtem Tap auf die bearbeitbare Schneide; zufällige Stimme im Sprachkanal | geliefert 2026-10-11 (`Assets/audio/laser/`) |
| Laser / Fail (Stimme) | `yell_high_01.wav`, `yell_high_02.wav`, `yell_high_03.wav`, `yell_high_04.wav`, `yell_high_05.wav` | 5 | Im Laser-Spiel nach Schafttreffer, falscher Bearbeitung oder Fehlschlag; zufällige Stimme im Sprachkanal | geliefert 2026-10-11 (`Assets/audio/laser/`) |
| Säge / Success (Stimme) | `cheer_low_01.wav`, `cheer_low_02.wav`, `cheer_low_03.wav`, `cheer_low_04.wav` | 4 | Im Säge-Spiel nach bestätigtem gültigem Schneiden-Swipe; zufällige Stimme im Sprachkanal | geliefert 2026-10-11 (`Assets/audio/grind/`) |
| Säge / Fail (Stimme) | `yell_low_01.wav`, `yell_low_02.wav`, `yell_low_03.wav`, `yell_low_04.wav`, `yell_low_05.wav` | 5 | Im Säge-Spiel nach falschem Swipe, Tap, Blattkörpertreffer oder Fehlschlag; zufällige Stimme im Sprachkanal | geliefert 2026-10-11 (`Assets/audio/grind/`) |
| Laser-Effekt | `laser_01.wav`, `laser_02.wav`, `laser_03.wav`, `laser_04.wav`, `laser_05.wav`, `laser_06.wav` | 6 | Im Laser-Modus bei jeder bestätigten Geste im Kurzfeedbackkanal; die Ergebnisunterscheidung übernimmt die Stimme | geliefert 2026-10-11 (`Assets/audio/laser/`) |
| Schleif-Effekt | `grind_01.wav`, `grind_02.wav`, `grind_03.wav`, `grind_04.wav` | 4 | Im Schleif-Modus bei jeder bestätigten Geste im Kurzfeedbackkanal; die Ergebnisunterscheidung übernimmt die Stimme | geliefert 2026-10-11 (`Assets/audio/grind/`) |
| UI / Bestätigung | `ui_confirm_01.mp3`, `ui_confirm_02.mp3`, `ui_confirm_03.mp3` | 3 | In der Betriebshülle bei akzeptierter Moduswahl, F4/F5, Info öffnen/schließen, Speichern/Überspringen, Ereignistasten, Betreiberaktion und F6 laut/leise | zu liefern oder selbst zu produzieren |
| UI / Ablehnung | `feedback_fail_01.wav`, `feedback_fail_02.wav`, `feedback_fail_03.wav` | 3 | Bei ungültiger Formulareingabe oder nicht zulässigem Befehl im Kurzfeedbackkanal; keine Punktewirkung | zu liefern oder selbst zu produzieren |
| Rundenstart | `round_start_01.mp3`, `round_start_02.mp3`, `round_start_03.mp3` | 3 | Einmal nach angenommenem F1-Start im Sprach-/Jinglekanal, statt UI-Klick | zu liefern oder selbst zu produzieren |
| Rundenende | `round_end_01.mp3`, `round_end_02.mp3`, `round_end_03.mp3` | 3 | Einmal beim serverbestätigten regulären Rundenende im Sprach-/Jinglekanal, mit Vorrang vor Stimmen | zu liefern oder selbst zu produzieren |
| Arcade-Musik | `bgm.mp3` | 1 | Nach Audiofreigabe beim Rundenstart ab 0:15 bis Ende als Loop; F6 und sanftes Ducking, Stopp bei Rundenende/Rollenverlust | geliefert, eingebunden; Hör-/Loopfreigabe offen |

Der Startvorschlag umfasst **41 Laufzeitdateien**: 28 gelieferte (18 Werkstückstimmen, 6 Laser-Effekte, 4 Schleif-Effekte) und **13 noch zu liefernde oder zu produzierende Dateien** (UI-Bestätigung, UI-Ablehnung, Rundenstart, Rundenende, Musik). Weitere Varianten sind willkommen, aber keine technische Voraussetzung. Keine separaten Schaft-/Fehlschlagpools, Ambientloops oder Gag-Sounds nötig. Ein Hörcheck der 28 gelieferten Dateien (Zuordnung, Pegel, Länge, Clipping) steht noch aus; er verschiebt höchstens die Manifestzuordnung, nie die Dateinamen.

Ein Anschauungspaket liegt unter [demo-material](../demo-material/README.md). Es dient ausschließlich der Abstimmung. **Seine technische und gestalterische Qualität ist ausdrücklich kein Maßstab für die Produktionsqualität des Games.** Seine früheren Beispieldateinamen sind keine zusätzliche Audiolieferliste; dafür gilt Kap. 3.4.4. Sein Beispielschema ist nicht bindend: Die Werkzeugbild- und Zonenstruktur folgt ausschließlich Kap. 3.4.5, die Zustandsdarstellung `stateOverlays` plus `tool-overlay`. Das Release benötigt ein validiertes Manifest, nutzbare Medienrechte und funktionierende Qualitäts-/Performanceprofile auf der Finalhardware, keinen separaten kreativen Freigabeprozess.

### 3.4.5 Werkzeugbild- und Trefferzonen-Regeln (`tool-image`)

Dieser Abschnitt regelt die Spielbarkeit der Werkzeugbilder (Bohrer, PKD-Fräser, Sägeblätter): Zonen, Größen und Zustände. Die Produktionsvorlage (Format, Leinwand, Ausrichtung, Lesbarkeit, Konsistenz), die Lieferliste mit zwölf Werkzeugen und die Sichtprüf-Checkliste stehen in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.1 und 3.7.1. Ein Asset ohne gültige Zonen gilt als vorlagenwidrig und blockiert die Manifestaktivierung. Warum Erfolg oder Misserfolg eintritt, regelt Kap. 3.3.1; die Zonen liefern dafür nur die Geometrie.

**Größenvarianten:** Die relative Werkzeuggröße wird über die Bildausnutzung statt über eigene Bildvorlagen gesteuert: `small` ≈ 45 %, `medium` ≈ 62 %, `large` ≈ 80 % der Leinwandkante (Werkzeug-Bounding-Box). Server-, Trefferzonen- und Effektgrößen skalieren gemeinsam mit dem Bild; dieselbe Bilddatei bedient mehrere Skalierungen.

**Trefferzonen (Pflicht):** Jedes Werkzeug besitzt im Manifest genau zwei Zonen:

| Zone | Rolle | Bedeutung | Wirkung im Laser-Modus | Wirkung im Schleif-Modus |
|-|-|-|-|-|
| `cut` | Bearbeitbare Schneide | Schneiden des Bohrers/Fräsers beziehungsweise markierte Sägezahnkante | `success` bei Tap | `success` bei gültigem Swipe |
| `shaft` | Schaft / Blattkörper | alles übrige opake Material des Werkzeugs | `shaft_hit` bei Tap | `wrong_sharpening` bei Tap oder Swipe |

- Zonen sind geschlossene Polygone in normalisierten Koordinaten `0.0` bis `1.0` relativ zur Werkzeug-Bounding-Box (Ursprung links oben); sie skalieren damit verlustfrei mit jeder Bildgröße.
- Die Zonen müssen die gesamte opake Silhouette lückenlos überdecken; eine Lücke ist ein Validierungsfehler. Bei Überlappung an der gemeinsamen Grenze hat `shaft` Vorrang.
- `cut` muss zusammenhängend sein und die tatsächlich bearbeitbare Kante abbilden, nicht den gesamten Werkzeugkopf. Eine leere oder flächenmäßig zu kleine `cut`-Zone ist ungültig.
- Für den Schleif-Modus ist keine Richtungs- oder Mindestlängenangabe erforderlich. Optionale Bestandsfelder `cutDirectionDeg` und `minStrokeLengthNormalized` haben keinen Einfluss auf die Wertung; eine Richtungsmarkierung ist nicht erforderlich.
- Zonen werden nicht aus Dateinamen abgeleitet, sondern ausschließlich im Manifest gepflegt.

**Zustandsdarstellung:** Das Bild liefert nur die Form und ist zustandsneutral. `stateOverlays` nennt nur, welche logischen Zustände (`dull`, `in_progress`, `sharp`, `failed`) ein Werkzeug unterstützt, nie eigene Bilddateien. Die Darstellung je Zustand liefert die `tool-overlay`-Kategorie (Kap. 3.5) mit `anchor: "cut-zone"`, gezeichnet über die `cut`-Zone; die konkrete Overlay-Liste und das Zustandsmodell stehen in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.2 und 3.5. `in_progress` ist eine kurze Übergangsdarstellung (≤ 600 ms) und keine eigene Spielanforderung.

**Effekte:** Laser- und Schleifeffekte verwenden die geteilten Atlanten `laser-effect` und `grind-effect`. Erfolg und Misserfolg unterscheiden sich ausschließlich über Tint (`green.600`/`red.600`) und Intensität, nicht über getrennte Effektatlanten.

**Konkreter Werkzeug-Startkatalog:** Die Startliste der Werkzeuge mit ID, Werkzeugtyp, Größenklasse, PKD-Anteil und Zonenbeschreibung steht in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.1. Der Katalog lässt sich dort ohne Kernlogikänderung erweitern; weitere Größen und PKD-Varianten sind zusätzliche Manifest-Einträge.

### 3.4.6 Bildproduktion mit Leonardo AI

Der Werkzeugbildsatz wird mit Leonardo AI erzeugt und anschließend normiert. Da Leonardo AI in der Regel ein flaches Rasterbild ohne echte Transparenz liefert, ist der Ablauf fest vorgegeben; die Bildqualität aus dem Generator ist Ausgangsmaterial, keine Freigabe. **Anzahl, IDs, Varianten und Zustandsbilder sind nicht Teil dieses Abschnitts**, sondern stehen in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.

**Ablauf pro Werkzeug:**

1. **Basisbild erzeugen:** Freistellen über eine uniforme, im Werkzeug nicht vorkommende Hintergrundfarbe (empfohlen Magenta `#FF00FF`), Format quadratisch (1:1), höchste verfügbare Auflösung; Stil- und Modellparameter einmal festlegen.
2. **Hintergrund entfernen:** Chroma-Key auf die gewählte Hintergrundfarbe anwenden und als transparentes PNG exportieren; Kanten mit 1 px weicher Alpha-Kante nacharbeiten. Bietet das gewählte Leonardo-AI-Paket eine Transparenz-/Freistellfunktion, kann sie den Chroma-Key ersetzen; die Normierung bleibt identisch.
3. **Normieren:** Auf die Werkzeug-Bounding-Box zuschneiden, 8 bis 10 % transparente Randreserve ergänzen, auf 1024 px längste Kante skalieren.
4. **Zonen setzen:** `cut` und `shaft` als normalisierte Polygone über das fertige Bild legen (Zonen-Editor aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md)) und als `<assetId>.zones.json` speichern.
5. **Manifest erzeugen:** Metadaten über `vgtools manifest seed`/`new asset` anlegen und das Manifest über `vgtools manifest build` generieren und validieren; kein Eintrag wird von Hand geschrieben.

**Prompt-Vorlage (englisch, damit der Generator stabil arbeitet):**

```text
flat comic vector illustration of a single industrial {tool_type}, {size_hint}, {pkd_hint},
orthographic side view, centered, bold clean outline, cel-shaded, playful retro arcade style,
consistent soft top-left lighting, solid uniform {chroma_color} background, full tool visible with margin,
no text, no watermark, no logos, no hands, no shadow on background, only one object
```

| Prompt-Platzhalter | Werte | Hinweis |
|-|-|-|
| `{tool_type}` | `twist drill bit`, `PKD-tipped milling cutter`, `circular saw blade` | genau ein Werkzeugbegriff pro Bild |
| `{size_hint}` | `small`, `medium`, `large` | die Größe im Spiel steuert die Bildausnutzung, nicht der Prompt |
| `{pkd_hint}` | `polycrystalline diamond cutting edges, faceted metallic sparkle` oder leer | nur für PKD-Varianten |
| `{chroma_color}` | `magenta` (bevorzugt) oder `green` | genau eine Farbe, die im Werkzeug nicht vorkommt |

**Negativ-Prompt (verbindlich):** `text, letters, numbers, watermark, logo, signature, multiple tools, collage, background scenery, floor, shadow, human hands, person, realistic photograph, blurry, cluttered details`.

| Parametergruppe | Vorgabe | Zweck |
|-|-|-|
| Seitenverhältnis | 1:1 | quadratische Leinwand für die Zonen-Normierung |
| Auflösung | höchste verfügbare Stufe | Reserve für 2048-px-Master und Skalierung |
| Modell/Stil | einmal festlegen, für alle Werkzeuge gleich | einheitliche Farbfamilie und Kontur |
| Seed | pro Werkzeug einmal festhalten | reproduzierbare Nachgeneration und PKD-Varianten |
| Varianten | 4 Bilder pro Werkzeug, bestes Silhouettenergebnis wählen | die Silhouette ist das wichtigste Kriterium |
| Image-Guidance | für PKD-Varianten mit niedriger Stärke aus dem Basisbild | gleiche Form, nur Detailänderung |

**Nachbearbeitungs-Checkliste:** Die verbindliche Sichtprüf-Checkliste für alle Bildassets steht in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.7.1 und wird dort geführt; eine zweite Checkliste wird bewusst nicht gepflegt.

**Nutzungsrechte:** Es gilt die geklärte Vorgabe aus OP-03: ausschließlich royaltyfree verwendbare oder eigene Inhalte. Der Generator muss die kommerzielle Nutzung der Ausgaben erlauben; der Lizenz-/Herkunftsstatus wird pro Asset im Manifest geführt (`licenseStatus`). Die Dateinamen bleiben reine Kategorie-IDs (`tool_0001.png`); die Bedeutung steht im Manifest.

**Showroom-Bühnenbild (`showroom-stage`):** Das Bühnenbild ist kein Werkzeugbild und folgt einer eigenen Vorgabe. Es wird wie die Werkzeuge mit Leonardo AI erzeugt, aber als 16:9-Kulisse ohne Freistellung; die inhaltlichen Regeln zur Zielfläche stehen in Kap. 3.5.

| Merkmal | Vorgabe |
|-|-|
| Format | Master 3840 × 2160 px (16:9), Lieferdatei WebP 2560 × 1440 px; kein Alpha nötig |
| Bildinhalt | Showroom-Innenraum: Hallenrückseite, orangefarbener Boden, einige fest aufgestellte Maschinen, Deckenlicht |
| Personen | **keine** Personen im Basisbild; Kollegen sind separate `showroom-cast`-Requisiten und werden nicht eingebrannt |
| Freie Zielfläche | eine zusammenhängende, detaill- und konturarme Zone `stageArea` im Boden-/Mittelfeld (Kap. 3.5) |
| Farbigkeit | Bodenorange unter `orange.600` abgesenkt; keine grellen Grün-, Rot- oder Cyansättigungen im Zielbereich |
| Kontrast | Zielbereich ohne harte Hell-/Dunkelkanten, ohne Schrift, Zahlen und Logos, die als Ziel missverstanden werden könnten |
| Stil | gleiche comicartige, cel-shadete Linienführung wie die Werkzeuge; keine Fotorealistik |

**Prompt-Vorlage (englisch):**

```text
wide comic vector illustration of an empty industrial showroom interior,
warm orange painted floor, anthracite machines standing along the walls and in the back,
ceiling light strips, clean uncluttered open floor in the foreground,
flat cel-shaded retro arcade style, bold clean outlines, soft top-left lighting,
no people, no text, no watermark, no logos, no tools in the foreground, empty stage area
```

**Negativ-Prompt (verbindlich):** `people, persons, hands, text, letters, numbers, watermark, logo, signature, tool in foreground, cluttered floor, harsh high-contrast shadows, realistic photograph, blurry`.

| Parametergruppe | Vorgabe | Zweck |
|-|-|-|
| Seitenverhältnis | 16:9 | deckt die logische Canvasfläche aus Kap. 3.7 |
| Auflösung | höchste verfügbare Stufe | Reserve für den 3840-px-Master |
| Seed | einmal festhalten | reproduzierbare Nachgeneration von Lichtvarianten |
| Varianten | 4 Bilder, danach Lichtvarianten per Image-Guidance aus dem Basisbild | gleiche Raumgeometrie, nur Lichtstimmung ändert sich |
| Nachbearbeitung | `stageArea` freistellen/abschwächen: keine Personen, keine Werkzeuge, keine harten Kanten im Zielbereich | die Zielfläche bleibt frei und lesbar |

### 3.4.7 Manifest: Zweck, Aufbau und Beispiel

**Was das Manifest ist:** Das Manifest ist **eine einzige, versionierte JSON-Datei** (geplant: `Assets/config/manifest.json`), die jedes Asset und jede Zuordnung deklarativ beschreibt: welche IDs vergeben sind, welche Datei dahinterliegt, welcher Vorlage/`category` der Eintrag folgt, welche Trefferzonen, Skalierungs- und Spawnprofile gelten, welche Audiopools zugeordnet sind und welcher Lizenzstatus vorliegt. Es ist die **einzige Quelle der Zuordnung**; Dateinamen und Codepfade tragen keine Bedeutung (Kap. 3.4). Im Manifest stehen keine Bildinhalte, Sounds oder Programme, sondern ausschließlich Verweise auf Dateien plus die fachlichen Felder, die die Laufzeit liest. Die Datei wird **generiert** und nicht von Hand geschrieben; wer sie erzeugt, welche kleinen Eingabedateien es gibt und wie die Werkzeugkette arbeitet, steht in [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md).

**Regeln:** Eine ID wird genau einmal vergeben und bleibt nach dem Entfernen reserviert. Jede im Manifest genannte Datei und jede Vorlage muss vorhanden und bekannt sein. Die Validierung läuft vor jeder Runde; bei Fehlern bleibt die letzte gültige Fassung aktiv. Werte wie Trefferzonen, Punkte, Poolgrößen oder die freie Zielfläche ändern sich durch einen neuen Manifesteintrag, nicht durch Codeänderung.

**Beispiel (verkürzt und nicht normativ vollständig):**

```json
{
  "manifestVersion": "1.0.0",
  "configVersion": "2026-09-28",
  "tools": [
    {
      "toolId": "tool_0001",
      "category": "tool-image",
      "toolType": "drill",
      "interaction": "tap",
      "sizeClass": "small",
      "pkd": false,
      "visualAssetId": "tool_0001.png",
      "pivot": { "x": 0.5, "y": 0.5 },
      "hitZones": [
        { "zoneId": "cut", "role": "cut", "points": [[0.30, 0.05], [0.70, 0.10], [0.66, 0.38], [0.34, 0.34]] },
        { "zoneId": "shaft", "role": "shaft", "points": [[0.34, 0.05], [0.66, 0.10], [0.60, 0.95], [0.40, 0.95]] }
      ],
      "stateOverlays": ["dull", "in_progress", "sharp"],
      "scoreProfileId": "laser_default",
      "audioPools": { "success": "laser_success", "fail": "laser_fail" },
      "licenseStatus": "royaltyfree"
    },
    {
      "toolId": "tool_0006",
      "category": "tool-image",
      "toolType": "saw_blade",
      "interaction": "swipe",
      "sizeClass": "small",
      "pkd": false,
      "visualAssetId": "tool_0006.png",
      "pivot": { "x": 0.5, "y": 0.5 },
      "hitZones": [
        { "zoneId": "cut", "role": "cut", "points": [[0.05, 0.42], [0.95, 0.38], [0.95, 0.58], [0.05, 0.55]] },
        { "zoneId": "shaft", "role": "shaft", "points": [[0.30, 0.05], [0.70, 0.05], [0.75, 0.95], [0.25, 0.95]] }
      ],
      "stateOverlays": ["dull", "sharp"],
      "scoreProfileId": "saw_default",
      "audioPools": { "success": "saw_success", "fail": "saw_fail" },
      "licenseStatus": "royaltyfree"
    }
  ],
  "audioPools": [
    {
      "poolId": "laser_success",
      "category": "laser-sfx",
      "variants": [
        "cheer_high_01.wav", "cheer_high_02.wav", "cheer_high_03.wav", "cheer_high_04.wav"
      ],
      "volumeProfile": "voice",
      "bagRule": "shuffle_no_repeat",
      "fallback": "laser_effect"
    },
    {
      "poolId": "laser_fail",
      "category": "laser-sfx",
      "variants": [
        "yell_high_01.wav", "yell_high_02.wav", "yell_high_03.wav",
        "yell_high_04.wav", "yell_high_05.wav"
      ],
      "volumeProfile": "voice",
      "bagRule": "shuffle_no_repeat",
      "fallback": "laser_effect"
    },
    {
      "poolId": "laser_effect",
      "category": "laser-sfx",
      "variants": [
        "laser_01.wav", "laser_02.wav", "laser_03.wav",
        "laser_04.wav", "laser_05.wav", "laser_06.wav"
      ],
      "volumeProfile": "feedback",
      "bagRule": "shuffle_no_repeat",
      "fallback": "generated_tone"
    }
  ],
  "stage": [
    {
      "assetId": "bg_0001",
      "category": "showroom-stage",
      "file": "images/background/bg_0001.webp",
      "layer": "backdrop",
      "parallaxFactor": 0.0,
      "stageArea": { "x": 0, "y": 0, "w": 1, "h": 1 },
      "palette": { "floor": "vollmer_orange_stage_dark", "wall": "grey_light" },
      "lightProfile": "showroom_day",
      "licenseStatus": "royaltyfree"
    },
    {
      "assetId": "cast_0001",
      "category": "showroom-cast",
      "file": "images/background/cast_0001.png",
      "sprite": "colleague_grey_polo",
      "layer": "props",
      "motionPath": "walk_back_band_left_to_right",
      "speedProfile": "walk_slow",
      "cooldownMs": 25000,
      "enabled": true
    }
  ],
  "spawnProfiles": [
    {
      "spawnProfileId": "stage_default",
      "minimumConcurrentTargets": 4,
      "maxStageCoverage": 0.45,
      "minTargetSpacingFactor": 1.25,
      "entryEdges": ["left", "right", "top", "bottom"],
      "difficultyRampId": "ramp_default"
    }
  ]
}
```

**Wie die Laufzeit das Beispiel liest:** `bg_0001` liefert die Kulisse, `stageArea` beschreibt die nutzbare Eingabefläche und `spawnProfiles.stage_default` die Zielzahl und vier Einflugseiten (Kap. 3.5). Außenflugpositionen dürfen außerhalb `0..1` liegen. Die bestehenden Vertragsfelder `maxStageCoverage` und `minTargetSpacingFactor` bleiben aus Kompatibilitätsgründen vorhanden, blockieren im Durchflugmodus aber weder Spawn noch Überlagerung. Das Produktionsmanifest ist weiterhin nicht im Spiel angebunden; dessen Übernahme bleibt ein offener Schritt aus AP-04. `cast_0001` ist eine rein dekorative Requisite ohne Trefferzone. `tool_0001` verweist über `audioPools` auf die beiden Laserpools; ein neuer Clip wird nur als zusätzliche `variant`-Datei plus Manifestzeile aufgenommen. Die verbindlichen Feldnamen stehen in Kap. 4; ein neues Hintergrund- oder Requisitenasset benötigt einen neuen Eintrag und keine Codeänderung. Das Beispiel dokumentiert den angestrebten Aufbau; die Datei entsteht aus Autorendateien und Werkzeugkette ([00003](00003-c-vollmer-manifest-und-hilfstools.concept.md)).

## 3.5 Hintergrund, räumliche Tiefe und Humor

**Favorit:** Das Spielbühnenbild ist eine feste Showroom-Kulisse des Vollmer-Messeraums: orangefarbener Boden, einige fest aufgestellte Maschinen und gelegentlich durchs Bild laufende Kollegen in grauen Vollmer-Polos. Die Kulisse steht still, die Werkzeuge bewegen sich davor; die räumliche Tiefe entsteht – wie bei „Moorhuhn“ – allein aus wenigen Bildebenen. Anders als dort gibt es keine Zieljagd, keine Kamera und kein Scrollen: Besucher müssen nichts bedienen, um die Welt zu sehen.

**Eine Kulisse, alle Ebenen:** Es gibt genau eine Basisdatei `bg_0001` (Kap. 3.4, Vorlage `showroom-stage`). Tiefe entsteht durch deren Bildebenen und durch optionale, bewegte Requisiten (`cast_*`) davor, nicht durch eine zweite Panoramavariante. Der Boden bleibt fest; nur Requisiten und Ziele bewegen sich.

| Ebene | Bewegung im Bild | Inhalt | Spielregel |
|-|-:|-|-|
| Kulisse (`bg_0001`) | fest ($0{,}00$) | Showroom-Wand, orangefarbener Boden, feste Maschinen, Deckenlicht | rein dekorativ; die freie Zielfläche `stageArea` bleibt detaillfrei |
| Requisiten (`cast_*`) | $0{,}10$ bis $0{,}25$ relativ zur Zielebene | gelegentlich laufender Kollege in grauem Polo, leise Maschinenbewegung | hinter der Zielebene gezeichnet, nie Zielverdeckung, nie Trefferzone |
| Zielebene | $1{,}00$ (Referenz) | Werkzeuge mit Silhouette, Trefferzonen und Zustands-Overlays | alle Ziele und Trefferzonen |
| Vordergrund | $1{,}10$ bis $1{,}25$ | Schutzscheibe, Kabeltraeger, Transportkorb, Foerdergut und Spänefilter | zeitlich begrenzte, regelbasierte Teilverdeckung |
| HUD | $0$ | Zeit, Punkte, Combo und Modus | nie von Weltobjekten verdeckt |

**Spielfläche (`stageArea`):** Für Durchflüge wird die gesamte Leinwand als Eingabefläche freigegeben (`{ x: 0, y: 0, w: 1, h: 1 }`). Die Kulisse bleibt rein dekorativ; Ziele dürfen vor Maschinen und Requisiten über das ganze Bild fliegen. Werkzeuge starten vollständig außerhalb einer zufällig gewählten Seite und verlassen das Bild auf der gegenüberliegenden Seite. Die ehemalige Begrenzung auf den freien Bodenbereich und auf zwei Einflugseiten entfällt. Lesbarkeit und Kontrast werden über die gesamte sichtbare Flugfläche geprüft.

**Mindestfüllung ohne Obergrenze:** Die Gameplay-Konfiguration hält standardmäßig mindestens `minimumConcurrentTargets: 4` registrierte Ziele vor. Fällt der Bestand darunter, wird im 250-ms-Spawntakt je ein Ziel ergänzt; eine maximale Zielzahl gibt es nicht. Auch noch außerhalb befindliche oder im kurzen Despawn befindliche Ziele zählen zum Bestand; dadurch kann die sichtbare Bühnenfläche während des Außenein- und -ausflugs vorübergehend weniger Ziele zeigen. Flüge laufen einmalig in 6–9 Sekunden durch und geben ihren Platz anschließend frei. Werkzeuge dürfen sich überschneiden; Flächenbelegung und Mindestabstand lösen weder Spawnsperre noch Ausweichen oder Umkehr aus. Die bisherigen Felder `maxStageCoverage` und `minTargetSpacingFactor` bleiben nur als kompatible Vertragsfelder erhalten. Die konkrete Bildschirmgröße je `sizeClass` steht im Skalierungsprofil, nicht im Prompt.

**Farbigkeit und Kontrast auf dem Boden:** Der Boden ist orange, aber bewusst **abgesenkt** (Sättigung und Helligkeit unter `orange.600`, Konfigurationswert). Ziele tragen ihre dunkle Kontur und bleiben dadurch lesbar; Cyan (Laser), Grün (Erfolg) und Rot (Fehler) bleiben auf dem Boden eindeutig. Die Kulisse verwendet im Zielbereich keine dieser Feedbackfarben als dominante Flächenfarbe.

Das Bühnenbild ist eine **Mischform aus fester Kulisse und optionalen Dekomodulen**: Kulisse, Werkzeuge und Kollegenrequisiten sind konkrete, mit Leonardo AI erzeugte Assets ([00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 und 3.7). Zusätzlich dürfen rein additive `background-module`-Dekorationen (Lichtlauf, Staub, Bodenreflex) aus konfigurierten Parametern entstehen. Sie liegen **über** der Kulisse, dürfen die `stageArea` aber weder ersetzen noch mit starkem Kontrast füllen und erzeugen keine Werkzeuge. Laser, Funken, Schleifspuren und kleine Ereignisse verwenden Atlas-/JSON-Animationen mit PNG/WebP-Texturen.

Verdeckungen sind bewusstes Gameplay, keine zufaellige Renderfolge: Jedes Ziel ist mindestens $650\text{ ms}$ voll sichtbar, maximal $40\,\%$ seiner Trefferflaeche darf fuer hoechstens $450\text{ ms}$ verdeckt sein, und die relevante Schneiden- beziehungsweise Tap-Zone bleibt sichtbar. In den ersten $15\text{ s}$ der Runde gibt es keine Verdeckung; zwischen zwei verdeckten Zielen liegen mindestens $2\text{ s}$. Eine ausbleibende Aktion auf ein verdecktes Ziel kostet weder Punkte noch Combo. Diese Regeln erhalten den humorvollen "gerade noch erwischt"-Moment, ohne die Messeinteraktion unfair zu machen. Kollegen und Maschinenrequisiten sind **keine** Verdeckung: Sie werden immer hinter der Zielebene gezeichnet und können deshalb keine Trefferzone abdecken.

Humor prägt Werkstückstimmen, Retro-Hülle und die gesamte Inszenierung. Zusätzliche kurze Gags in der industriellen Welt bleiben spielregelneutral. Beispiele sind eine uebermotiviert vorbeifahrende Qualitaetsmarkierung, ein zu schnell rollender Schraubenkorb, ein kurz aufblinkendes "scharf genug"-Pruefsiegel oder eine Schutzscheibe, die ein Ziel theatralisch freigibt. Jeder Gag hat ein eigenes sichtbares Ereignis, eine Manifest-ID, ein konfiguriertes Spawnprofil und eine optionale Audioereignisgruppe. Er darf weder Trefferzonen veraendern noch notwendige Gesten verschleiern und ist jederzeit per Konfiguration deaktivierbar. Das Startpaket benötigt dafür keine zusätzlichen Sounds über Kap. 3.4 hinaus. Der Kollege in grauem Vollmer-Polo ist der ruhigste dieser Gags: Er läuft ausschließlich in der hinteren Bildebene, hat einen konfigurierten Cooldown, ist nie Trefferzone und ist im Betreiberprofil abschaltbar.

| Neue Assetkategorie | Zweck | Pflichtfelder im Manifest |
|-|-|-|
| `showroom-stage` | feste Showroom-Kulisse als Spielbühne | `file`, `layer`, `parallaxFactor`, `stageArea`, `palette`, `lightProfile` |
| `showroom-cast` | gelegentlich bewegte, rein dekorative Requisite (Kollege in grauem Polo, Maschinendetail) | `file`, `layer`, `motionPath`, `speedProfile`, `cooldownMs`, `enabled` |
| `background-module` | additives, optionales Dekomodul über der Kulisse (Lichtlauf, Staub, Bodenreflex) | `layer`, `parallaxFactor`, `tileWidth`, `palette`, `spawnProfile` |
| `foreground-occluder` | zeitweise sichtbarer Vordergrundgegenstand | `maxCoveredTargetArea`, `maxDurationMs`, `cooldownMs`, `motionProfile` |
| `tool-overlay` | stumpfe Schneide, Fortschritt oder geschärfte Kante | `toolId`, `state`, `anchor`, `blendMode` |
| `humor-event` | rein dekorative, spielregelneutrale Nebeninszenierung | `trigger`, `cooldownMs`, `visualAssetId`, `audioEvent`, `enabled` |
| `event-sfx` | gewichtete, kurze Audioantwort auf ein Ereignis | `event`, `variants`, `volumeProfile`, `fallback` |
| `ambient-loop` | dezente Hallen- oder Maschinenatmosphaere | `zone`, `loopPoints`, `volumeProfile`, `fallback` |

Die Zuordnung von Bild zu Ton erfolgt ausschließlich ueber das Manifest, nie ueber Dateinamen oder feste Codepfade. Ein `humor-event` referenziert beispielsweise `audioEvent: "occluder_reveal_comic"`; diese Ereignisgruppe verweist auf eine oder mehrere gewichtete `event-sfx`-IDs. Fehlt die Gruppe oder eine Datei, bleibt die sichtbare Nebeninszenierung erhalten und der Runtime-Fallback bleibt stumm. Damit koennen Content-Verantwortliche neue Gags, Hintergrundvarianten und Tonvarianten per neuem Asset plus validiertem Manifesteintrag aktivieren, ohne die Kernlogik anzupassen.

## 3.6 Retro-Editor-Betriebshuelle und Spielinszenierung

**Favorit:** Die historische Vollmer-Editorreferenz wird als klar erkennbare, modern touchfaehige Betriebshuelle nachgebildet. Das feste Anthrazitgehaeuse, die Showroom-Buehne (Kap. 3.5), die linke Maschinen-/Achsspalte, die rechte grune Ereignisleiste und die untere F-Tastenleiste rahmen das Spiel, ersetzen aber weder die Phaser-Spiellogik noch die direkte Geste auf dem Werkzeug.

| Bereich | Aufgabe | Verbindliche Regel |
|-|-|-|
| Showroom-Buehne (Spielcanvas) | Einziger Spielcanvas fuer die Showroom-Kulisse, Ziele, Treffer, Partikel und leichte Parallaxe | Kein Bedienelement verdeckt die Flaeche; die Touchgeste findet ausschliesslich hier statt. Die Kulisse steht fest und ueberdeckt keine Trefferzone. |
| Linke Statusspalte | Kompakte Retro-Anzeige fuer Programm, Punkte, verbleibende Zeit, Combo und virtuelle Achsen | Achswerte sind rein visuelles Feedback und werden aus bestaetigten oder lokal vorhergesagten Aktionen animiert; sie sind keine Eingabe. |
| Linke Maschinenflaeche | Comicartige Bearbeitungszelle mit sichtbarer Aufhaengung fuer Laser und Schleifscheibe | Die Maschine bleibt ausserhalb der Trefferzone und zeigt nur nachvollziehbare, kurze Bewegungen zur jeweiligen Aktion. |
| Rechte Ereignisleiste | Acht grune Symboltasten fuer harmlose Nebeninszenierungen | Waerend einer Runde loesen Tasten nur lokale, konfigurierbare Bild-/Toneffekte ohne Punkte-, Combo- oder Rollenwirkung aus. |
| Untere Funktions- und F-Tastenleiste | Oranges `START`-Feld und blaue Funktionsfelder ueber sechs grauen Retro-Tasten | `F1` ist ein nicht rastender orangefarbener Starttaster; `F2` bis `F6` sind visuell rastende Schalter. Die Funktionsfelder tragen die Klartextbefehle, die grauen Tasten nur `F1` bis `F6`. |

Der humoristische Sketch ist die visuelle Referenz fuer Kontrast, Proportionen und die komische Werkzeuginszenierung; die Medienherkunft wird davon getrennt dokumentiert. Die Bedienleiste verwendet keine historischen Produktionsbefehle unveraendert, damit Besucher nicht auf eine echte Maschinensteuerung schliessen. Das orange Feld `START` markiert die einzige primäre Aktion; die blauen Funktionsfelder tragen `LASER`, `SCHEIBE`, `ANLEITUNG`, `HIGHSCORE` und `TON`. Die darunterliegenden grauen F-Tasten tragen nur `F1` bis `F6` und sind eine visuelle Retro-Metapher. `F1` ist der einzige Momenttaster und startet nur aus dem Attract- oder Ergebniszustand. `F2` und `F3` sind gegenseitig ausschliessende, rastende Modusschalter: Genau einer ist sichtbar gedrueckt und bestimmt die Runde sowie die zugehoerige Rangliste. `F4` und `F5` oeffnen ausserhalb einer laufenden Runde modale Textfenster und sind waehrend des Spiels gesperrt; `F4` zeigt die Spielanleitung im selben Stil wie die Rangliste, `F5` die getrennte Rangliste des aktiven Modus. `F6` ist der rastende Schalter fuer den gesamten lokalen Mix aus Musik, Werkstückstimmen und Bedienfeedback zwischen laut, leise und stumm. Betreiberbefehle bleiben ausserhalb dieser Besucherleiste und folgen weiterhin dem geschuetzten Langdruck mit Slide-to-confirm.

Die Ranglisten `LASER` und `SCHEIBE` sind getrennte, lokal persistent gespeicherte Bestenlisten; ein Eintrag besteht aus Modus, Punktzahl, Anzeigename, Zeitstempel, Hinweis-/Einwilligungsversion und einer technischen Eintrags-ID. Ein Fantasiename genügt. E-Mail, Gewinnspielteilnahme und gegebenenfalls Einwilligung zur Vertriebsansprache werden getrennt von der öffentlichen Rangliste gespeichert. Nur Einträge mit E-Mail und Gewinnspielteilnahme werden bei der Preisvergabe nach der Messe berücksichtigt; die öffentliche Rangliste enthält auch Einträge ohne E-Mail. Betreiber können die erforderlichen Kontakt-/Gewinnspieldaten geschützt für die Nachbearbeitung exportieren, einzelne Einträge oder beide Ranglisten löschen. Exporte sind nicht für Watcher erreichbar und unterliegen denselben Zweck- und Löschregeln. Ohne Eintrag, nach Formular-Timeout oder bei Abbruch werden keine eingegebenen Kontaktdaten gespeichert. Wiederherstellung darf gelöschte/abgelaufene Daten nicht erneut veröffentlichen; Details regelt Kap. 3.8.

Die linke Comicmaschine zeigt als technische Andeutung einen kurzen Portaltraeger mit Schlitten, Kabelschlauch und gemeinsamer Werkzeugaufnahme. Der Laser ist ein schmaler Fokuskopf unter der Aufnahme; die Schleifscheibe haengt seitlich daneben an einem kurzen Schwenkarm mit sichtbarem Schutzring. Im Laser-Modus faehrt der Schlitten entlang einer virtuellen $X$-Achse, richtet sich aus und sendet einen kurzen cyanfarbenen Strahl sichtbar in die Spielflaeche. Im Scheiben-Modus schwenkt der Arm vor, die Scheibe dreht an und bleibt mit ihrer Schutzhaube klar als bearbeitendes Werkzeug lesbar. Der Swipe ueber die markierte Werkzeugschneide wird als lineare Zustellung gelesen: Die $X$-Anzeige folgt der Wischrichtung, $Z$ zeigt einen kurzen Zustellimpuls, und ein schmaler Funkenstreifen laeuft entlang der Schneide. Dadurch bleibt die Ninja-Slice-Qualitaet erhalten, ohne dass Besucher eine Scheibe frei zeichnen oder die Maschine selbst steuern muessen.

Die rechten Symboltasten sind optionale, wertungsneutrale Werkstatt-Spielereien: Messuhr-Zucken, Förderband-Kurzlauf, Schutzscheiben-Wischer, roter `OK`-Prüfstempel, Werkzeugwechsel, grünes Prüfsiegel, Antriebsbewegung und eine kurze Logo-Überraschung. Jede Taste hat ein eindeutiges Piktogramm, einen maximal $2\text{ s}$ langen Effekt und einen konfigurierbaren Cooldown. Die Überraschung wird standardmäßig erst nach drei verschiedenen harmlosen Ereignistasten freigeschaltet; nach ihrer Auslösung beginnt die Folge erneut. Ereignisse verdecken keine Ziele oder Navigation und ändern weder Punkte, Combo noch Spielerrolle. Die Taste `F6 TON` und die Systemlautstärke gelten auch für alle Spielereien.

Die Referenz ist keine pixelgenaue Reproduktion: Auf Handhelds werden Lesbarkeit und Zielgroessen gegenueber dekorativer Dichte priorisiert. Die Spalten und F-Tasten werden proportional mit festen Mindestgroessen skaliert; bei schmalem Hochformat wird die rechte Ereignisleiste als ausklappbare Symbolschublade angezeigt, waehrend die Spielflaeche (Showroom-Buehne) mindestens $70\,%$ der Breite erhaelt. Der Watcher uebernimmt die Showroom-Buehne und eine grossformatige Maschinenanimation, zeigt jedoch weder F-Tasten noch Ereignistasten als bedienbar an.

## 3.7 Lokaler Windows-Betrieb und verbindlicher Kabel-Fallback

**Favorit:** Windows 11 auf einem Fujitsu LifeBook U9313X, lokal paketierter ASP.NET-Core-Host inklusive Client, Schriften, Assets und Konfiguration sowie installierter Chromium-basierter Browser (bevorzugt Edge). Die Serienangaben der [Zielhardware](https://laptopmedia.com/series/fujitsu-lifebook-u9313x/) nennen ein 13,3-Zoll-Full-HD-Display, HDMI 2.0, zwei Thunderbolt-4-/DisplayPort-fähige USB-C-Anschlüsse und eine 3,5-mm-Kombibuchse. Windows 10 bleibt ein gesondert auf dem konkreten Gerät zu prüfendes Kompatibilitätsziel, keine ungeprüfte Supportzusage. Framework-, Browser- und Betriebssystemversionen werden passend zur Zielhardware festgeschrieben. Keine Installation, Lizenzaktivierung oder Assetabfrage darf beim Messe-Kaltstart Internet benötigen.

| Betriebsart | Aufbau | Verbindlichkeit |
|-|-|-|
| Einzelgerät | Server, Betriebshülle und aktiver Phaser-Client auf demselben LifeBook U9313X, Kommunikation über Loopback | muss bei deaktiviertem WLAN und gezogenem Netzwerkkabel vollständig funktionieren |
| Kabelanzeige | Direkte HDMI-2.0-Verbindung zum 27-Zoll-QHD-Monitor bevorzugt; Thunderbolt-4-/DisplayPort-USB-C nur als geprüfter Ersatzweg. Erweiterter Windows-Desktop, zweites lokales Browserfenster als stummer Watcher | verbindlicher Fallback, kein Netzwerk und kein zweiter Rechner nötig |
| Drahtloser Beobachter | separates Gerät mit passivem Browserclient über eine vor Ort verfügbare lokale Verbindung | optional; Ausfall oder fehlende Erlaubnis der Funkverbindung verhindert keine Runde |

Die Kabelanzeige ist bewusst eine lokale Zuschaueransicht statt einer dauerhaften 1:1-Spiegelung: So bleiben Bildschirmtastatur, Datenschutzeinwilligung und E-Mail-Eingabe auf dem Convertible. Windows „Duplizieren“ ist nur für den technischen Bildtest ohne Personendateneingabe geeignet. Im regulären Betrieb wird „Erweitern“ verwendet; bei unklarer Monitorzuordnung bleiben Personendatenfelder gesperrt und das Spiel kann ohne Eintrag weiterlaufen.

**Auflösung:** Der aktive Host nutzt 1920 × 1080, 16:9 quer, auf dem 13,3-Zoll-Display. Der passive 27-Zoll-Monitor nutzt QHD mit 2560 × 1440, ebenfalls 16:9 quer. Canvas und Trefferkoordinaten nutzen dieselbe logische 16:9-Fläche; der Watcher rendert sie nativ in QHD, andere Seitenverhältnisse erhalten Letterboxing statt verzerrter Werkzeuge. Touchflächen bleiben mindestens 44 × 44 CSS-Pixel groß. Full HD am Host bei 100 %, 125 % und 150 % Windows-Skalierung sowie QHD am Watcher bei der festgelegten Skalierung sind verbindliche Layout-Prüfbasis, keine bereits bestandene Hardwareabnahme.

**Start und Fallback:** Netzteil und Kabel anschließen, Windows-Anzeigemodus prüfen, lokalen Host starten, aktive Ansicht auf dem Touchdisplay und lokalen Watcher auf dem zweiten Monitor öffnen, Tonprobe mit fest gewähltem Ausgabegerät durchführen. Fällt Funk aus, läuft die Runde weiter; der Kabel-Watcher kann sich jederzeit mit einem Snapshot zuschalten. Monitor-Abziehen oder -Anstecken darf weder aktive Rolle noch Timer ändern. HDMI/USB-C kann das Windows-Audioziel umschalten: bevorzugt bleibt der getestete lokale Lautsprecher-/Klinkenausgang fest gewählt; Monitoraudio wird nur nach eigenem Anschlusstest verwendet. Ein Audio-Selbsttest nach dem Umstecken gehört zur Betriebscheckliste.

Standardmäßig bindet der Host nur an Loopback. Eine externe Schnittstelle wird nur für den optionalen Beobachterbetrieb bewusst aktiviert, mit lokaler Firewallbegrenzung, vertrauenswürdig eingerichtetem HTTPS und kurzlebiger Watcher-Berechtigung. Fernclients erhalten serverseitig ausschließlich Leserechte, niemals eine aktive oder Betreiberrolle. Kein Besucher-WLAN und kein eigener Access Point sind Voraussetzung des Spielbetriebs.

## 3.8 Datenschutz-Infobutton und konfigurierbares Overlay

**Festgelegt:** Ein kleines `i` im Kreis in der festen Bedienleiste öffnet „Datenschutz“. Das sichtbare Symbol ist klein, seine Touchfläche mindestens 44 × 44 CSS-Pixel; zugänglicher Name und Tooltip lauten „Datenschutz“. Es ist vor der ersten Dateneingabe sowie im Highscoreformular erreichbar. Nur der aktive Client zeigt das Overlay, niemals der Watcher.

Das modale Overlay besitzt eine begrenzte Höhe innerhalb des verfügbaren Viewports, einen eigenen vertikal scrollbar bedienbaren Textbereich und eine jederzeit sichtbare Schließen-Schaltfläche. Touch-Scroll, Mausrad und Tastaturnavigation funktionieren auch bei Bildschirmtastatur und 200 % Browserzoom. Fokus bleibt im Dialog und kehrt beim Schließen zum Auslöser zurück; Escape schließt, Gesten werden nicht an den Spielcanvas durchgereicht. Der Rundentimer läuft weiter. Während einer bewussten Formular-/Overlayinteraktion läuft kein Eingabe-Timeout ab; nach Inaktivität werden ungespeicherte Eingaben verworfen.

**Konfiguration ohne Neubau:** Vorgesehen ist `Assets/config/privacy.de.json` mit `version`, `controller`, `contact`, `dataProtectionContact`, `purposes`, `legalBases`, `recipients`, `retention`, `rights`, `supervisoryAuthority` und Textabschnitten für den Hinweis. Das E-Mail-Feld ist sichtbar und aktiviert; sein Zweck ist Lead-Erfassung und Preisbenachrichtigung nach der Messe. `leaderboardDeleteAt`, `prizeDataDeleteAt`, `leadDataDeleteAt`, Teilnahmebedingungen und Einwilligungstexte werden ebenfalls konfiguriert. Keine Skripte oder beliebiges HTML, sondern UTF-8-Text und strukturierte Absätze. Neuladen validiert Pflichtfelder und aktiviert die neue Version vor dem nächsten Formular ohne Build; offene Formulare behalten ihre Version bis zum Abschluss. Ungültige Konfiguration lässt die letzte gültige Fassung aktiv. Ohne gültigen Hinweis bleibt das Spiel ohne personenbezogene Speicherung nutzbar.

**Festgelegter Ablauf:** Das Spiel dient der Lead-Erzeugung und dem Gespräch mit potenziellen Kunden. Im Ergebnisformular werden Name und E-Mail angeboten; ein Name oder Fantasiename ist für den gespeicherten Highscore erforderlich. E-Mail ist für das Spielen und die Rangliste optional, für die Preisvergabe nach der Messe dagegen erforderlich. Leere E-Mail ist beim Speichern eines reinen Highscores zulässig, eine ausgefüllte Adresse wird auf plausibles Format geprüft. Eine Formatprüfung garantiert keine Erreichbarkeit: Preise werden erst nach erfolgreicher Kontaktaufnahme vergeben. Fantasienamen sind auch mit E-Mail erlaubt; es wird kein amtlicher Name verlangt.

**Zwecke und Rechtsgrundlagen:** Die Veröffentlichung des Anzeigenamens mit dem Highscore erfolgt mit freiwilliger Einwilligung nach Art. 6 Abs. 1 lit. a DSGVO. Wer am Gewinnspiel teilnimmt, bestätigt die verlinkten, ebenfalls konfigurierbaren Teilnahmebedingungen; die dafür erforderliche E-Mail-Verarbeitung und spätere Gewinnbenachrichtigung dienen der Durchführung nach Art. 6 Abs. 1 lit. b DSGVO. Für weitergehende vertriebliche E-Mail-Ansprache gibt es eine separate freiwillige, nicht vorangekreuzte Einwilligung. Ihre Ablehnung verändert weder Spiel, Highscore noch Gewinnchance. Eine reine Gewinnspieladresse ist keine pauschale Werbeerlaubnis; der Betreiberexport unterscheidet Gewinnspielkontakte und für Vertriebsansprache freigegebene Leads. Kein Newsletter und kein automatischer E-Mail-Versand aus dem Spiel sind vorgesehen.

E-Mail und Einwilligungsnachweise erscheinen nie auf dem Watcher oder in Diagnose-Logs. Der Server speichert Zweck, Hinweisversion und Zeitpunkt; für die Löschung gibt es einen nur dem Spieler angezeigten Löschcode. `Ohne Eintrag weiter` bleibt möglich. Ein Widerruf der Vertriebsansprache beendet nur diese Verarbeitung, nicht automatisch die unabhängig davon gewünschte Gewinnspielteilnahme.

### 3.8.1 Konkreter Hinweistext als Konfigurationsvorlage

> **Datenschutz beim Vollmer Messe-Game**
>
> Verantwortlich ist [vollständige juristische Person, postalische Anschrift]. Kontakt: [Kontaktadresse]. Datenschutzkontakt beziehungsweise Datenschutzbeauftragte/r, soweit benannt: [Kontaktdaten].
>
> Mit dem Messe-Game möchten wir mit potenziellen Kunden ins Gespräch kommen. Du kannst einen Namen oder Fantasienamen angeben und auch ohne E-Mail spielen sowie einen Highscore speichern. Für die Rangliste verarbeiten wir Anzeigename, Spielmodus, Punktestand, Zeitpunkt, technische Eintragskennung und Einwilligungsnachweis. Nur Anzeigename, Punktestand und Rang sind am Messestand und auf Zuschaueranzeigen sichtbar; deine E-Mail-Adresse bleibt vertraulich.
>
> Die Veröffentlichung deines Highscores erfolgt mit deiner freiwilligen Einwilligung nach Art. 6 Abs. 1 lit. a DSGVO. Möchtest du an der Preisvergabe teilnehmen, benötigen wir eine erreichbare E-Mail-Adresse und deine Bestätigung der Teilnahmebedingungen. Wir ermitteln und benachrichtigen Gewinner erst nach der Messe und verwenden die Adresse zur Gewinnabwicklung nach Art. 6 Abs. 1 lit. b DSGVO. Ohne E-Mail-Adresse ist kein Preis möglich; spielen und ein Fantasiename in der Rangliste bleiben möglich.
>
> Nur wenn du zusätzlich zustimmst, verwenden wir deinen Namen und deine E-Mail-Adresse für die vertriebliche Kontaktaufnahme zu deinem Interesse an Vollmer-Produkten nach der Messe. Grundlage ist Art. 6 Abs. 1 lit. a DSGVO. Diese Zustimmung ist freiwillig und hat keinen Einfluss auf deine Gewinnchance. Einwilligungen kannst du jederzeit beim Standpersonal oder über [Kontaktadresse] mit Wirkung für die Zukunft widerrufen; die Verarbeitung bis dahin bleibt rechtmäßig. Mit deinem Löschcode können wir deinen Eintrag zuordnen.
>
> Wir speichern die Daten zunächst lokal auf dem Messegerät. Für Gewinnabwicklung und von dir erlaubte Vertriebsansprache übernimmt [zuständiges Team beim Verantwortlichen] die jeweils erforderlichen Daten über einen geschützten Export. Weitere Empfänger beziehungsweise eingesetzte E-Mail-Dienstleister: [konkrete Angaben]. Drittlandübermittlungen: [keine oder konkrete Angaben einschließlich Schutzmaßnahmen]. Das Spiel selbst verwendet kein Tracking und keine Cloudverbindung.
>
> Ranglistendaten löschen wir am [Datum]. Gewinnspielkontakte löschen wir nach Abschluss der Gewinnabwicklung spätestens am [Datum]; für vertriebliche Kontaktaufnahme freigegebene Leads spätestens am [Datum], sofern nicht inzwischen eine eigenständige Kundenbeziehung mit gesondertem Verarbeitungszweck entstanden ist. Einwilligungsnachweise und gegebenenfalls gesetzlich aufzubewahrende Nachweise speichern wir für [konkrete Fristen und Gründe]. Die Regeln gelten auch für Exporte und lokale Sicherungen.
>
> Du hast nach den gesetzlichen Voraussetzungen Rechte auf Auskunft, Berichtigung, Löschung, Einschränkung der Verarbeitung und Datenübertragbarkeit sowie, soweit einschlägig, Widerspruch. Du kannst dich bei [zuständige Datenschutzaufsichtsbehörde, Anschrift/Internetadresse] beschweren. Für Anfragen erreichst du uns unter [Kontaktadresse].

**Formulartexte, jeweils ohne Vorbelegung:**

- Rangliste: „Ich möchte meinen Anzeigenamen und mein Ergebnis in der am Messestand sichtbaren Rangliste speichern lassen. Diese Einwilligung kann ich jederzeit widerrufen.“
- Gewinnspiel: „Ich möchte an der Preisvergabe nach der Messe teilnehmen und akzeptiere die Teilnahmebedingungen. Dafür gebe ich eine erreichbare E-Mail-Adresse zur Gewinnbenachrichtigung an.“
- Vertriebsansprache, separat freiwillig: „Vollmer darf mich nach der Messe unter meiner E-Mail-Adresse zu meinem Interesse an Vollmer-Produkten kontaktieren. Diese Einwilligung kann ich jederzeit widerrufen.“

Der Datenschutzhinweis ist vor Speichern erreichbar; Öffnen oder Schließen des Infotexts ersetzt keine Einwilligung. Das Formular weist direkt am E-Mail-Feld darauf hin: „Ohne E-Mail kannst du mitspielen; einen Preis gibt es nur mit erreichbarer E-Mail-Adresse.“

### 3.8.2 Umsetzung der geklärten Datenschutzvorgabe

**Bevorzugtes Aufbewahrungsprofil:** Keine pauschale Löschung direkt nach Messeende, bevor Gewinner kontaktiert wurden. Ranglisten und Gewinnspielkontakte bis zum Abschluss der Preisabwicklung zuzüglich 30 Tagen vorhalten; für erlaubte Vertriebsansprache erfasste Leads ohne weiterführende Kundenbeziehung höchstens sechs Monate nach Messeende. Diese Startwerte werden als konkrete zweckbezogene Löschtermine konfiguriert; erforderliche Nachweise mit eigener begründeter Frist getrennt führen. Ein Widerruf der Vertriebsansprache ist unverzüglich zu berücksichtigen. Bei Neustart werden überfällige Daten vor Anzeige gelöscht. Gerät, Daten und Exporte durch Windows-Konto, restriktive Dateirechte und Datenträgerverschlüsselung schützen. Betreiber können einzelne oder alle Einträge löschen; Exporte und Sicherungen sind in die Löschung einzubeziehen.

**OP-04 ist fachlich geklärt.** Die Platzhalter für Verantwortlichen, Kontakt, Empfänger, Termine und Aufsichtsbehörde werden bei der Einrichtung ausgefüllt, ohne neue Konzeptentscheidung oder Neubau. Die Vorlage orientiert sich an Art. 13 der [DSGVO](https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=de); der geklärte Konzeptstatus ist keine rechtliche Zertifizierung. Hinweistext, tatsächliche Nutzung und technische Umsetzung müssen übereinstimmen.

# 4. Contracts

## 4.1 tl;dr
- Der Server ist die einzige Autorität für Rolle, Rundentimer, Punktestand und validierte Treffer.
- Clients senden versionierte Befehle, erhalten bestätigte Ereignisse und bei Reconnect vollständige Snapshots.
- Ein Watcher bleibt passiv und kann ohne Einfluss auf die Runde jederzeit wieder beitreten.

| Schnittstelle | Richtung | Datenstruktur / Protokoll |
|-|-|-|
| Sitzungsbeitritt | Client -> Server | `JoinRequest { ClientInstanceId, RequestedRole, ResumeToken? }`; lokal gleichursprünglich über Loopback, externer Watcher über HTTPS/WebSocket. Antwort enthält kurzlebige Berechtigung und `RoleVersion`; aktive Rolle nur mit lokaler Betreiber-Provisionierung, nicht allein aufgrund von `RequestedRole`. |
| Spieleraktion | Aktiver Client -> Server | `ActionRequested { RoundId, CommandSequence, ToolId, ActionKind, InputTimestamp, Gesture }`; `ActionKind` ist `tap` oder `swipe`, `Gesture { Kind, Points[], StartedAtMs, EndedAtMs }`. |
| Bestätigung | Server -> alle Clients | `ActionResolved { RoundId, EventId, StateVersion, Outcome, HitZone, AudioEvent, ScoreDelta, ToolState, ServerTimestamp }`; identisch wiederholbar. `Outcome` unterscheidet `success`, `wrong_sharpening`, `shaft_hit`, `miss` und wird allein nach Kap. 3.3.1 bestimmt; `HitZone { ZoneId, Role }` nennt die getroffene Zone (`cut` oder `shaft`, leer bei `miss`). `AudioEvent` ist je Modus ausschließlich `laser_success`, `laser_fail`, `saw_success` oder `saw_fail` und wählt den Stimmenpool; zusätzlich startet der aktive Client das modusbezogene Effektsignal `laser_effect`/`grind_effect` (Kap. 3.4.2). Audio nur am aktiven Client, genau einmal je `EventId`. |
| Zustandssnapshot | Server -> Client | `GameSnapshot { RoundId, StateVersion, Phase, ActiveClientId, Mode, RemainingMs, Score, Combo, Tools }`; bei Join, Reconnect und Versionslücke. |
| Highscore / Kontakt | Aktiver Client -> Server | `HighscoreSubmitted { RoundId, Name, LeaderboardConsent, PrivacyVersion, Email?, PrizeParticipation, TermsVersion?, SalesContactConsent }`; Name oder Fantasiename, Modus/Punkte aus der Serverrunde. Gewinnspielteilnahme verlangt E-Mail und bestätigte Bedingungen; Vertriebsansprache verlangt separat E-Mail und Einwilligung. Ohne beides wird keine zwecklose E-Mail gespeichert. Öffentliche Antwort nur Anzeigename, Punktzahl und Rang; Löschcode nur privat. Überspringen/Timeout speichert nichts. |
| Nachmesse-Auswertung | Betreiber -> lokaler Server | geschützter Export von Gewinnspielkontakten beziehungsweise eingewilligten Leads, einschließlich Zweck, Eintrags-ID und Hinweis-/Bedingungs-/Einwilligungsversion. Keine Watcherberechtigung; Gewinnberechtigung serverseitig prüfen, keine E-Mail in öffentlichen Ranglisten. |
| Betrieb | Blazor-Shell -> Server | lokaler Diagnose-/Resetvertrag; der Betreiber-Slide für Freigabe/Abbruch erfordert Betreiberberechtigung und erhöht `RoleVersion`, bei Abbruch zusätzlich `RoundId`. |
| Konfiguration | Host -> Clients | versionierte Gameplay-, Asset-, Audio- und Datenschutzkonfiguration: Laufzeit, Punkte, Trefferzonen, Dateipools, Zufallsregel, Mischerlimits, Ducking, Hinweistext und Löschregeln; validiertes Neuladen zwischen Runden ohne Neubau. |
| Werkzeug-/Zonenmanifest | Host -> Server und Clients | `ToolManifestEntry { ToolId, ToolType, Interaction, SizeClass, Pkd, VisualAssetId, Pivot, HitZones[], StateOverlays[], ScoreProfileId, AudioPools, LicenseStatus }`; `HitZone { ZoneId, Role: "cut"|"shaft", Points[] }` in normalisierten Koordinaten relativ zur Werkzeug-Bounding-Box. Der Validator erzwingt genau eine `cut`- und eine `shaft`-Zone, lückenlose Silhouettenabdeckung, `Interaction` `tap`/`swipe` sowie vorhandene Bild- und Audioreferenzen; bei Fehlern bleibt die letzte gültige Fassung aktiv. Dasselbe Manifest führt die Kategorien `showroom-stage`, `showroom-cast` und `spawnProfiles` (Beispiel in Kap. 3.4.7); der Validator erzwingt dort das Vorhandensein einer `stageArea` mit gültigen Grenzen innerhalb `0..1` und der Mindestfläche aus Kap. 3.5. Die Datei wird generiert (siehe [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md)); das Schema bleibt hier verbindlich. |

Ein lokaler ASP.NET-Core-Dienst verwendet SignalR/WebSocket für Ereignisse und Snapshots. Transportzustellung ersetzt keine Fachlogik: Der Server dedupliziert `ClientInstanceId + CommandSequence`, prüft `RoundId` und aktive Rolle und beantwortet Wiederholungen idempotent. Ein atomarer Rollenwechsel entzieht zuerst die alte Rolle, erhöht `RoleVersion`, weist dann die neue Rolle zu und sendet einen Snapshot. Die Betreiberaktion `ReleaseRole` ist außerhalb einer Runde zulässig; `AbortRoundAndReleaseRole` beendet die aktive Runde ohne Highscoreeintrag.

# 5. Implementierungsplan

## 5.1 tl;dr
- Die Umsetzung beginnt unmittelbar mit einem vertikalen Prototyp; Hardwareprofil und Medienvertrag sind festgelegt, ihre Nachweise erfolgen erst vor Messebetrieb.
- Phaser/TypeScript plus lokaler ASP.NET-Core-Server ist der Favorit, weil Touch-Reaktion und Betriebsverantwortung sauber getrennt bleiben.
- Die Retro-Editor-Huelle ist eine Clientdarstellung mit phasengebundenen Befehlen; sie aendert weder die autoritative Wertung noch die passive Watcherrolle.
- Die nachfolgenden Arbeitspakete sind verbindlich und unverändert in der Fortschrittsseite geführt.

| AP | Ziel / Scope | Betroffene Komponenten | Abhängigkeiten | Validierung | Done-Kriterium |
|-|-|-|-|-|-|
| AP-01 | Betriebsnachweise für Messeaufbau vorbereiten. | LifeBook-U9313X-/27-Zoll-QHD-Profil, HDMI-Primärweg, USB-C-Ersatzweg, Audioausgang, schlanke Werkzeugbild-/Zonen-/Medien-/Datenschutzliste | Kann parallel zu AP-02 bis AP-05 erfolgen; für die Messefreigabe vor AP-06 abschließen | Checkliste gegen Konzept prüfen; B-01 und B-02 als Nachweise führen | Prüffähige Betriebscheckliste liegt vor; kein Implementierungsschritt ist davon abhängig. |
| AP-02 | Reproduzierbares Projektgerüst und lokalen Offline-Host aufsetzen. | .NET-Solution, ASP.NET Core, Blazor-Betriebshülle, TypeScript/Phaser, Windows-Paketierung | — | sauberer Build; Kaltstart mit deaktiviertem WLAN und ohne WAN | Ein Startablauf öffnet Host und aktiven Client auf demselben Convertible sowie optional einen lokalen Kabel-Watcher. |
| AP-03 | Autoritativen Runden-, Punkte-, Rollen- und Synchronisationskern implementieren. | C#-Domäne, SignalR, Trefferzonen-Klassifikation (`cut`/`shaft`), Konfiguration, Snapshots, MSTest | AP-02 | Tests für 60-s-Standard, konfigurierbare Dauer, Zonenpriorität/-toleranz, Fehl-/Schaftabzug, Rundengrenzen, Deduplizierung und passive Fernclients | Genau ein lokaler Client ist aktiv; jede Geste zählt höchstens einmal, es gibt genau ein Ergebnis je Geste (Kap. 3.3.1), negative Punkte beenden keine Runde und Fernclients bleiben passiv. |
| AP-04 | Tap-Laser, Säge-Swipe und humorvolle Retro-Editor-Hülle prototypisch umsetzen. | Phaser, Showroom-Kulisse `bg_0001` mit `stageArea`, Kollegenrequisiten `cast_*`, Werkzeugbilder mit PKD-Varianten, Zonen-Editor und generiertes Manifest ([00003](00003-c-vollmer-manifest-und-hilfstools.concept.md)), Zustands-Overlays aus der `cut`-Zone, Sägeblätter, leichte Parallaxe, F-Tasten, lokale Watcher-Ansicht | AP-03 | Touch-, Zonen-/Silhouettenabdeckungs-, Zustands-, Bühnen-/Ziellast-/Kontrast-, Full-HD-Host-/QHD-Watcher-/DPI-, Verdeckungs- und lokale Latenztests | Trefferzonen decken die Werkzeugsilhouette lückenlos ab; Erfolg, falsches Schärfen, Schafttreffer und Fehlschlag sind je Geste eindeutig und der Werkzeugzustand (`dull`, `in_progress`, `sharp`, `failed`) ist sichtbar korrekt; die `stageArea` bleibt auch bei maximaler Zielzahl frei, detaillarm und lesbar; die lokale Bildrückmeldung erfüllt das Budget, der 27-Zoll-QHD-Kabel-Watcher zeigt dieselbe Runde ohne Eingaben. |
| AP-05 | Inhalte, Audiopools, Musikmischer, Highscores und Lead-/Gewinnspielerfassung ausbauen. | Manifest, Success-/Fail-Pools, Zufallsbeutel, Ducking, Kanalgrenzen, VWC-Tokenzuordnung, Datenschutz-Overlay/-Konfiguration, Ranglisten, Kontaktformular und Betreiberexport | AP-04; Audio-Platzhalter zulässig, Lieferstatus aus B-02 bleibt sichtbar | Audio-Burst-/Poolgrößen-/Ducking-, Token-/Kontrast-, Overlay-, Konfigurationswechsel-, Persistenz-, Preisberechtigungs-, Einwilligungs-, Export- und Löschtests | Rundenfluss, zwei Effekte maximal, orange Hauptaktionen, semantisch eindeutige Feedbackfarben, Musik-Ducking, Hinweis ohne Neubau, Fantasienamen ohne E-Mail und Preisvergabe nur mit E-Mail sowie getrennte Vertriebsfreigabe sind umgesetzt. |
| AP-06 | Messehärtung und verbindlichen Kabel-Fallback auf Originalhardware nachweisen. | Windows-Kiosk, Monitor-/Audioumschaltung, Recovery, finale Medien, Betriebsdokumentation | AP-05; B-01 und B-02; geklärte Vorgaben aus OP-03/OP-04 umsetzen | Kaltstart ohne Netz, Kabel-Hotplug, optionaler Funkverlust, vier Stunden Dauerlauf, Bild-/Zonencheck, Bühnenbild-/Ziellastcheck, Hörcheck und Datenschutzprüfung | Offline- und Kabelbetrieb sind nachgewiesen, Showroom-Bühnenbild mit freier Zielfläche, Werkzeugbilder und Trefferzonen sowie Produktionssounds übernommen/geliefert/geprüft, Datenschutzhinweis konfiguriert und Nachmesse-Auswertung sowie Wiederanlauf geprobt. |

| Option | Vorteile | Nachteile |
|-|-|-|
| A: Blazor Server als vollständiger Spielclient | Durchgängig C#, zentraler Zustand | Touch und Rendering hängen an einer Dauerverbindung; für Partikel/Canvas unnötig fragil. |
| B: Reines Blazor WASM | Offlinefähiges Browser-Frontend, C#-Kenntnisse | Kein Vorteil für den Game-Loop; Mehrgeräte-Autorität bleibt zusätzlich nötig. |
| C: Blazor-Shell + ASP.NET Core + TypeScript/Phaser | Lokaler C#-Betrieb, performanter Canvas-Loop, browserfähige Handhelds, klare Verantwortung | Zwei Frontend-Toolchains benötigen festgeschriebene Builds. |
| D: Reines TypeScript/Phaser | Kleiner Spielstack | Kiosk, Diagnostik und C#-Integration müssen separat gelöst werden. |

**Favorit:** Option C.

**Begründung:** Phaser ist für Canvas, Partikel, Audio und Touch-Gesten der direkte Laufzeitfit; ASP.NET Core und Blazor geben dem überwiegend C#/C++-erfahrenen Team eine robuste Heimat für Regeln, Diagnose und lokalen Messebetrieb. Gegenüber A bleibt die unmittelbare Touch-Reaktion vom Netz entkoppelt; gegenüber D erhält der Betrieb einen klaren, wartbaren Host.

Empfohlene Werkzeuge: Phaser als 2D-Laufzeit-Engine, TypeScript für Clientcode, .NET/ASP.NET Core für Server und Shell, optional Blender für 3D-Quellmodelle, Figma oder vergleichbares Tool für UI und eine DAW für selbst produzierte Audioassets. Medienherkunft und Nutzungsrechte bleiben nachvollziehbar, ohne formale kreative Freigabeschleife. C++ bleibt für einen später nachgewiesenen nativen Algorithmus reserviert; keine Browser-DLL-Annahme.

# 6. Tests

## 6.1 tl;dr
- Der Schwerpunkt liegt auf messbaren Touch-, Synchronisations-, Performance- und Messebetriebsprüfungen auf echter Zielhardware.
- C#-Regeln werden mit MSTest abgedeckt; Client- und Browserflüsse erhalten automatisierte E2E-Checks sowie manuelle Hardwareabnahme.
- Kein Akzeptanzwert gilt als erfüllt, bevor er in einer reproduzierbaren Konfiguration gemessen wurde.

| Bereich | Prüffall / Akzeptanzkriterium |
|-|-|
| Rundenlänge | Standardrunde endet serverautoritativ nach $60\text{ s}$; konfigurierbarer Bereich lässt nur $30$ bis $90\text{ s}$ zu. |
| Touch | Tap und Swipe erzeugen lokal sichtbares Feedback in $\leq 50\text{ ms}$ p95 auf dem Zielhandheld. |
| Framerate | Aktiver Client erreicht $\geq 55\text{ FPS}$ p95, Watcher $\geq 50\text{ FPS}$ p95 unter dem festgelegten Maximalspawnprofil. |
| Hintergrund und Verdeckung | Die Showroom-Kulisse bleibt fest; Kollegen- und Maschinenrequisiten laufen ausschließlich hinter der Zielebene und verdecken nie ein Ziel. Jedes Ziel ist mindestens $650\text{ ms}$ vor der ersten Verdeckung vollständig sichtbar; keine Verdeckung überschreitet $40\,\%$ Trefferfläche oder $450\text{ ms}$. |
| Showroom-Bühne und Ziellast | Die gesamte Leinwand ist als Eingabefläche freigegeben; vier zufällige Flugrichtungen starten und enden außerhalb des Bildes, auch bei Rotation. Der Spawner füllt einen Bestand unter der konfigurierten Mindestzahl im 250-ms-Takt auf; es gibt keine maximale Zielzahl. Außeneinflug kann die unmittelbar sichtbare Zielzahl kurzzeitig unterschreiten. Durchflüge enden ohne Rücklauf oder Punkteabzug; gleichzeitig berührte Überlagerungen werden nach sichtbarer Z-Reihenfolge gewertet. Lesbarkeit, Konturen, Framerate und Feedback werden auch bei hoher Ziellast auf Zielhardware geprüft. |
| Retro-Editor-Huelle | Die Showroom-Buehne ist der einzige Ziel- und Gestenbereich; linke Achsen reagieren sichtbar auf Laser und Schleifswipe, ohne selbst Eingabe zu werden. |
| Design-Tokens und Kontrast | HUD, Bedienhülle und Overlays verwenden die semantische VWC-Zuordnung: Orange für Fokus/Hauptaktion, Cyan für Laser, Grün für Erfolg und Rot für Fehler; `yellow.600` bleibt Shell- und Weltakzent. Der orangefarbene Bühnenboden ist ein abgesenkter Weltwert und macht weder Fokus-/Hauptaktionsorange noch Fehlerrot verwechselbar; Ziele behalten auf dem Boden ihre dunkle Kontur. Touch- und Tastaturfokus bleiben auf anthrazitfarbenem Gehäuse sichtbar; Spielwelt und Effekte bleiben innerhalb der Lesbarkeitsregeln gestalterisch frei. |
| Funktions- und F-Tastenleiste | `F1` ist ein nicht rastender Starttaster. `F2` bis `F6` sind ausreichend grosse, visuell rastende Schalter; `F2 LASER` und `F3 SCHEIBE` bleiben stets gegenseitig ausschliessend, genau einer ist aktiv. Die grauen Tasten enthalten ausser `F1` bis `F6` keinen weiteren Text. Alle rechten Ereignistasten sind wertungsneutral und respektieren `TON`. |
| Laser- und Schleifscheiben-Inszenierung | Die linke Maschine zeigt Portaltraeger, Schlitten, Kabelschlauch, Fokuskopf sowie eine seitliche Scheibe mit Schutzring. Ein Swipe entlang der sichtbaren Schneide zeigt gerichtete Achsbewegung, Scheibenanlauf und Funkenstreifen, ohne eine freie Maschinensteuerung oder unklare Trefferzone zu erzeugen. |
| Kleine Viewports | Im schmalen Hochformat bleibt die Spielflaeche (Showroom-Buehne) mindestens $70\,%$ breit; die Ereignisleiste wechselt ohne Ueberlappung in eine Symbolschublade. |
| Highscore und Neustart | Name oder Fantasiename plus Ranglisteneinwilligung genügen, E-Mail darf leer bleiben; Überspringen/Timeout gibt ohne Speicherung frei. `LASER` und `SCHEIBE` bleiben getrennt. Gültige Einträge überstehen einen Neustart; gelöschte/abgelaufene nicht. E-Mail fehlt in öffentlichen DTOs, Logs und Kabelanzeige. |
| Leads und Preisvergabe | E-Mail-Feld ist aktiv; leerer Wert verhindert nur Gewinnspiel-/E-Mail-Kontaktteilnahme, nicht Spiel/Highscore. Ungültige ausgefüllte Adresse wird beanstandet. Preisberechtigte Exportliste enthält ausschließlich Teilnehmer mit E-Mail und bestätigten Bedingungen. Vertriebsansprache nur bei separater Einwilligung; deren Ablehnung verändert die Gewinnchance nicht. Daten sind nach Messeende noch zur Preisabwicklung vorhanden; Export nur für Betreiber. |
| Synchronisierung | Befehl bis Server p95 $\leq 75\text{ ms}$, bestätigter Clientzustand p95 $\leq 150\text{ ms}$ lokal; optionaler Netzwerk-Watcher p95 $\leq 250\text{ ms}$ im gewählten Netz. Funkverlust beeinflusst lokale Wertung und Kabelanzeige nicht. |
| Rollen | Zwei parallele Beitrittsversuche ergeben stets genau eine aktive Rolle; nur der Betreiber-Slide kann freigeben oder abbrechen, und jeder Wechsel ist atomar und versioniert. |
| Paketverlust | Wiederholte Nachricht und Versionslücke ändern Punktestand/Ereignis nie doppelt; Snapshot stellt Konsistenz wieder her. |
| Offline und Kabel | Kaltstart, Runde, Rangliste, lokale Watcher-Ansicht, Musik und Assets funktionieren mit deaktiviertem WLAN und gezogenem Netzwerkkabel. Externen Bildschirm vor/während/nach der Runde anschließen und abziehen: keine zweite aktive Rolle, kein Timerreset, keine Veröffentlichung privater Formulare. Audioausgang nach Hotplug prüfen. |
| Full HD, QHD und Skalierung | Aktiver LifeBook-Host: 1920 × 1080 bei 100/125/150 % Windows-Skalierung; passiver 27-Zoll-Watcher: 2560 × 1440 bei festgelegter Skalierung. UI, Canvas und Trefferzonen stimmen überein. Andere Seitenverhältnisse verzerren keine Zonen, Mindesttouchflächen und Letterboxing bleiben erhalten. |
| Wertung | Erfolg +10, falsches Schärfen -5, Schaft -10, Fehlschlag -5 als Startprofil; geänderte Werte ohne Build aktivierbar. Zonengrenzen, PKD-/Nicht-PKD- und Größenvarianten prüfen; ein Versuch zählt einmal, negative Stände enden erst mit dem Timer. |
| Werkzeugbild und Trefferzone | Die Vorgaben stammen aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 und 3.7.1: genau ein Werkzeug auf transparentem Grund mit 8 bis 10 % Rand und bei 96 px erkennbarer Silhouette; die Zonen `cut` und `shaft` decken die Silhouette lückenlos ab, `cut` ist zusammenhängend und nicht leer, und an der gemeinsamen Grenze wertet `shaft`. |
| Werkzeugzustände | Alle vier Zustände sind sichtbar unterscheidbar: `dull` beim Spawn, `in_progress` als kurzer Übergang, danach genau einmal `sharp` oder `failed`; `sharp` und `failed` lösen bei erneutem Kontakt kein Ergebnis, keine Punkte und keinen Sound aus, und `failed` verschwindet beschleunigt. |
| Erfolg-/Fehler-Entscheidung | Die Tabelle aus Kap. 3.3.1 liefert je Modus und Geste genau ein `Outcome`: Tap in `cut` = `success`, Tap in `shaft` = `shaft_hit`, falsche Richtung/zu kurzer Swipe/Tap im Schleif-Modus = `wrong_sharpening`, Geste ohne Kontakt = `miss`; nur `success` startet eine Success-Stimme, Schaft/Fehlbearbeitung/Fehlschlag teilen die Fail-Stimme; zusätzlich startet jede bestätigte Geste das modusbezogene Effektsignal (Kap. 3.4.2). |
| Bildsatz-Konsistenz und Leonardo-Ablauf | Alle Werkzeuge nutzen dieselbe Leinwandgröße, Lichtrichtung und Kontur; Größenvarianten skalieren mit den Zonen; Chroma-Key/Freistellung erzeugt saubere Alpha-Kanten; eine erneute Generierung mit festgehaltenem Seed reproduziert die Form. Kulisse und Kollegenrequisiten folgen derselben Linienführung; Personen sind separate Alpha-Requisiten und nicht in `bg_0001` eingebrannt. |
| Audioereignisse und Pools | Je Modus nur `success` und `fail` als Stimmenpools; Schaft/Fehlbearbeitung/Fehlschlag teilen den Fail-Pool. Jedes bestätigte Ereignis startet zusätzlich das modusbezogene Effektsignal. Poolgrößen 1, 4, 6 und 8 ohne Codeänderung prüfen, leerer Pool nutzt Fallback. Alle Varianten vor erneutem Mischen einmal ziehen; ab zwei Varianten keine direkte Wiederholung. Wiederholte Event-ID, Snapshot und Reconnect sind stumm. |
| Audio-Burst und Ducking | Zehn Aktionen in einer Sekunde: höchstens zwei Effektquellen plus Musik, keine wachsende Queue, veraltete Stimmen nach spätestens 200 ms verwerfen. Musikabsenkung 3 dB, Attack 80 ms, Hold 120 ms, Release 250 ms; keine kumulative Absenkung, kein unbeabsichtigtes Entstummen. Kurzfeedback p95 höchstens 100 ms nach Gestenende. |
| Audiofehler und Ausgabe | Fehlende/defekte Sprachdatei erhält das modusbezogene Effektsignal, ein fehlendes Effektsignal nutzt den Ersatzton; keine Musik blockiert keine Runde. Autoplay-Sperre, F6 laut/leise/stumm und Wechsel des Windows-Audioziels prüfen; beide Watcherarten bleiben stumm. Finale Dateien anhören und Pegel/Clipausschnitte prüfen. |
| Datenschutz-Overlay | Kreis-i vor Dateneingabe erreichbar; langer Hinweis vollständig mit Touch/Maus/Tastatur scrollbar, Schließen bei 200 % Zoom und Bildschirmtastatur sichtbar. Fokusführung, Escape, keine Canvas-Durchgriffe, keine Timeout-Löschung beim aktiven Lesen. |
| Datenschutz-Konfiguration und Löschung | Textwechsel ohne Neubau ab nächstem Formular, alte Version für offene Formulare; ungültige Fassung aktiviert keine Speicherung. Ranglisten-, Gewinnspiel- und Vertriebszwecke getrennt validieren. Widerruf der Vertriebsansprache lässt unabhängig gewünschte Gewinnspielteilnahme bestehen. Löschcode, zweckbezogene Fristen nach Preisabwicklung, Exporte und Sicherungswiederherstellung prüfen. |
| Asset-Austausch | Ein validiertes Manifest nimmt ein neues Asset mit nächster freier Kategorie-ID und passender Vorlage vor einer Runde ohne Kernlogikänderung auf; doppelte, fehlende oder vorlagenwidrige IDs blockieren die Aktivierung. |
| Bild-/Audio-Mapping | Ein neues Hintergrund-, Humor- oder Ereignisasset referenziert seine optionale Audioereignisgruppe allein im Manifest; bei fehlendem Audio bleibt das Bildereignis ohne Unterbrechung sichtbar. |
| Fallback | Watcher-Neustart beeinflusst die Runde nicht; Handheld-Reconnect folgt der definierten Frist; Serverausfall führt zu kontrolliertem Neustart oder getrenntem Demo-Modus. |

Unit-Tests sichern Punkte-, Combo-, Zeit-, Rollen- und Deduplizierungsregeln ab. Integrationstests prüfen Hub-/Snapshotverträge, Konkurrenz und Reconnect. E2E-Tests decken Start, Gesten, Runde, Ergebnis und Watcherbeitritt ab. Zusätzlich sind ein Dauerlauf über mindestens vier Stunden, ein Paketverlusttest, ein Kaltstarttest und eine Vor-Ort-Abnahme mit finalen Geräten verpflichtend.

### 6.2 Betriebs- und Mediennachweise vor Messebetrieb

Diese Nachweise sind keine offenen Konzeptentscheidungen und blockieren weder Projektgerüst noch Spielimplementierung. Sie sind ausschließlich Eintrittskriterien für AP-06 und den Messebetrieb.

| Nachweis | Inhalt | Verantwortlicher Zeitpunkt |
|-|-|-|
| B-01 Hardware und Audioausgabe | Auf dem tatsächlichen LifeBook und 27-Zoll-QHD-Monitor: HDMI-Primärweg, USB-C-Ersatzweg, Windows-Skalierung, Touch, Hotplug sowie der fest gewählte Audioausgang funktionieren im Netz-aus-Test. „Audioabnahme“ bedeutet ausschließlich diese technische Wiedergabeprüfung einschließlich Lautstärke und Umschaltverhalten, keine kreative oder fachliche Freigabe. | Vor AP-06 |
| B-02 Produktionsmedien | Die Bildlieferliste aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 ist vollständig geliefert: zwölf Werkzeugbilder, zwölf Zustands-Overlays, Bühnenbild, Requisiten, Verdecker und Effektatlanten, jeweils mit gültigen Zonen, sowie Sprach-, UI- und Musikdateien. **Stand 2026-10-11:** Die 28 Sounddateien aus Kap. 3.4.1 und 3.4.2 (18 Werkstückstimmen, 6 Laser-Effekte, 4 Schleif-Effekte) sind geliefert; offen sind `ui_confirm_*`, `feedback_fail_*`, `round_start_*`, `round_end_*` und `arcade_music_01.mp3` (13 Dateien) sowie die komplette Bildlieferung. Der Bild-/Zonencheck folgt der Sichtprüf-Checkliste aus 00002 Kap. 3.7.1 (ein Objekt pro Bild, transparente Kante auf orangefarbenem Grund, Mindestrand, Silhouettenlesbarkeit bei 96 px, lückenlose `cut`/`shaft`-Abdeckung, Overlay-Deckung in allen Größenklassen); der Bühnenbildcheck prüft 16:9, Mindestfläche und Detaillarmut der `stageArea`; der Hörcheck prüft Zuordnung, Pegel, Loopgrenzen und Clipping. Das Manifest ist über die Werkzeugkette aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) erzeugt und validiert. Platzhalter und technisch leere Pools bleiben bis dahin für die Implementierung zulässig. | Vor AP-06 |

# 7. Risiken

| Risiko | Kurzbeschreibung | Risikohöhe |
|-|-|-|
| R-01 | Ungeprüfte Touch-, Monitor- oder Audioanschlüsse gefährden den Kabelbetrieb. | hoch |
| R-02 | Selbst ergänzte Fremdmedien könnten von der geklärten royaltyfree-/Eigenkreationsvorgabe abweichen. | mittel |
| R-03 | Hohe Partikeldichte oder ungetestete Browserkonfiguration senkt die Bildrate. | mittel |
| R-04 | Serverausfall unterbricht die gemeinsame Wertungsrunde. | mittel |
| R-05 | Überlappende Stimmen, Autoplay-Sperren oder Audio-Umschaltung zerstören das hörbare Feedback. | mittel |
| R-06 | Gewinnspieladressen könnten ohne separate Einwilligung für Vertriebsansprache verwendet oder vor Preisabwicklung gelöscht werden. | hoch |
| R-07 | Die Showroom-Kulisse könnte Ziele verdecken oder den Zielbereich zu unruhig und kontrastarm machen. | mittel |

## 7.1 R-01: Messehardware und Kabelbetrieb
### tl;dr
- **Favorit:** Original-Convertible, Monitor, Adapter und Audioausgang gemeinsam ohne Netz testen.

### Detail
Der aktive Spielpfad hängt nicht am WLAN. Touchpanel, DPI-Skalierung, Anschlussadapter und Windows-Audioumschaltung bleiben jedoch konkrete Ausfallquellen. Zwei lokale Browseransichten belasten dieselbe GPU.

### Empfehlung
- **Favorit:** Windows 11, erweiterten Desktop, festen Audioausgang und geprüften Ersatzadapter verwenden; Kaltstart und Hotplug in AP-06 nachweisen.
- **Begründung:** Genau diese Kombination sichert den verbindlichen Kabel-Fallback unabhängig vom Messefunk.

## 7.2 R-02: Medienrechte ohne Freigabeprozess
### tl;dr
- **Favorit:** Die bestätigte royaltyfree-/Eigenkreationsvorgabe auch auf selbst ergänzte Assets und Schriften anwenden.

### Detail
Der Nutzer pitcht und bearbeitet selbst und verwendet ausschließlich royaltyfree Quellen oder eigene Kreationen/Kompositionen. Diese Grundlage ist geklärt; es besteht keine offene Rechtefrage zur Konzeptentscheidung. Bei neuen Fremdassets gelten deren konkrete Nutzungsbedingungen auch dann, wenn sie als royaltyfree angeboten werden.

### Empfehlung
- **Favorit:** Nutzervorgaben übernehmen; für eigene Ergänzungen frei verwendbare Gestaltung und Schriften wählen, nötige Lizenzhinweise mitliefern.
- **Begründung:** Das hält die geklärte Mediengrundlage ein, ohne weitere Freigaberunde oder Beschaffungspflicht.

## 7.3 R-03: Performance der Effekte
### tl;dr
- **Favorit:** Partikel- und Spawnobergrenzen profilbasiert konfigurieren und auf Finalhardware testen.

### Detail
Laser, Funken und große Werkzeugtexturen können mobile GPUs überlasten.

### Empfehlung
- **Favorit:** Qualitätsprofile mit festen Obergrenzen, Object Pooling und Performancebudget je Zielgerät.
- **Begründung:** Visuelle Qualität bleibt steuerbar, ohne die Spielregeln oder Assets zu duplizieren.

## 7.4 R-04: Ausfall des autoritativen Servers
### tl;dr
- **Favorit:** Kontrollierter Neustart mit vorbereitetem Ersatzhost statt komplexem Hochverfügbarkeitscluster.

### Detail
Ohne Server ist eine gemeinsame wertungsrelevante Runde nicht zuverlässig fortsetzbar.

### Empfehlung
- **Favorit:** Lokale, atomare Zustandsablage nur für Diagnose/Restart, ein getesteter Ersatzhost und klarer Neustartfluss.
- **Begründung:** Das reduziert Messekomplexität und ist für eine zeitlich begrenzte Installation zuverlässiger als Live-Failover.

## 7.5 R-05: Audioüberlastung und Stille
### tl;dr
- **Favorit:** Einen gemeinsamen Mixer mit Kurzfeedback, einer Stimme und einem Musikstream einsetzen.

### Detail
Schnelle Eingaben können Stimmen stapeln; Browser-Autoplay und HDMI-Hotplug können einen vorhandenen Audiopfad stummschalten oder umlenken.

### Empfehlung
- **Favorit:** Limits und Ducking aus Kap. 3.4.3 verbindlich implementieren, Watcher stummschalten und einen lokalen Tontest anbieten.
- **Begründung:** Das kurze Aktionsfeedback bleibt erhalten, während Stimmen und Musik kontrollierbar bleiben.

## 7.6 R-06: Datenschutz im tatsächlichen Betrieb
### tl;dr
- **Favorit:** Gewinnabwicklung und weitergehende Vertriebsansprache mit getrennten Zwecken und Aufbewahrungsfristen umsetzen.

### Detail
Lead-Erzeugung und Preisbenachrichtigung nach der Messe sind festgelegt. Eine reine Gewinnspieladresse darf nicht automatisch als allgemeine Werbeeinwilligung behandelt werden; vorzeitige Löschung würde die nachgelagerte Preisvergabe verhindern.

### Empfehlung
- **Favorit:** Den Disclaimer aus Kap. 3.8 einsetzen, Preisvergabe an erreichbare E-Mail binden, Vertriebsansprache separat erlauben lassen und Daten erst nach zweckbezogenen Fristen löschen.
- **Begründung:** Das unterstützt Leads und Nachmesse-Kontakt, ohne Fantasienamen-Spieler auszuschließen oder eine unbegrenzte Werbeerlaubnis zu unterstellen.

## 7.7 R-07: Showroom-Kulisse und Lesbarkeit der Ziele
### tl;dr
- **Favorit:** Durchflüge über die gesamte Leinwand zulassen, bei niedrigem Bestand mindestens vier Ziele nachführen und Kontrast sowie Framerate bei hoher Ziellast prüfen.

### Detail
Ein vollständiges Showroom-Bild ist reizvoll, kann aber Ziele überdecken oder deren Kontrast senken. Weil die Kulisse fest und bildschirmfüllend ist, hängt die Lesbarkeit an der Bildkomposition (freier Bodenbereich) und an der Zieldichte, nicht an einer Kamera.

### Empfehlung
- **Favorit:** Die volle Leinwand gemäß Kap. 3.5 verwenden, Kollegen hinter die Zielebene legen und mindestens vier registrierte Ziele nachführen; den Bühnen-/Ziellastcheck in AP-04 und B-02 um frei kreuzende Ziele, hohe Ziellast und Trefferpriorität erweitern.
- **Begründung:** Einmalige Durchflüge verhindern dauerhaft belegte Bahnen; dunkle Konturen und sichtbare Trefferpriorität halten die gewünschte Überlagerung spielbar. Eine Obergrenze wird nicht gesetzt; Framerate und Lesbarkeit sind deshalb ausdrücklich bei hoher Ziellast zu prüfen.

# 8. Offene Punkte

| Offener Punkt | Kurzbeschreibung | Status |
|-|-|-|
| OP-01 | Zielhardware, Auflösung und Anschlusspfade | ✅ geklärt |
| OP-02 | Lokaler Betrieb und Kabel-Fallback; Funk nur als Option | ✅ geklärt |
| OP-03 | Eigene/royaltyfree Medien; ohne Nutzervorgabe frei verwendbare Farben und Schriften | ✅ geklärt |
| OP-04 | Leads, Fantasienamen und Preisbenachrichtigung per E-Mail nach der Messe; konfigurierbarer DSGVO-Hinweis | ✅ geklärt |
| OP-05 | Fach-/Marketingfreigabe entfällt ausdrücklich | ✅ geklärt |
| OP-06 | Bestehender Stack auf einem Windows-Gerät | ✅ geklärt |
| OP-07 | Assetvertrag und Übernahmeregeln | ✅ geklärt |
| OP-08 | Werkzeugbilder und Trefferzonen mit Leonardo AI | ✅ geklärt |
| OP-09 | Showroom-Bühnenbild, freie Zielfläche und Zieldichte | ✅ geklärt |

## 8.1 ✅ OP-01: Zielhardware und Aufstellungsdetails
### tl;dr
- Festgelegt: Fujitsu LifeBook U9313X als Windows-Host mit 13,3-Zoll-Full-HD-Display; Server und aktiver Client laufen auf demselben Gerät. Ein 27-Zoll-QHD-Monitor zeigt kabelgebunden den passiven Watcher.
- Festgelegt: HDMI 2.0 ist der Primärweg, Thunderbolt-4-/DisplayPort-USB-C der Ersatzweg. Serienkonfiguration, Monitor-/Skalierungseinstellung und technische Audioprüfung sind Betriebsnachweise B-01, keine Entwurfsentscheidungen.

### Detail
Die [Serienspezifikation des LifeBook U9313X](https://laptopmedia.com/series/fujitsu-lifebook-u9313x/) führt 1920 × 1080 für das integrierte Display sowie HDMI 2.0 und zwei Thunderbolt-4-/DisplayPort-fähige USB-C-Anschlüsse. Der 27-Zoll-Monitor ist als QHD-16:9-Watcher mit 2560 × 1440 festgelegt. Ein separates Handheld oder ein externer Server ist nicht erforderlich. Die konkrete CPU-/RAM-/SSD-/Windows-Konfiguration kann innerhalb der Serie variieren; sie wird in B-01 erfasst, ohne die Implementierung zu beeinflussen.

### Empfehlung
- **Favorit:** Windows 11 auf dem LifeBook nutzen, den 27-Zoll-QHD-Monitor direkt per HDMI 2.0 anbinden und USB-C nur als getesteten Ersatzweg bereithalten.
- **Begründung:** Der direkte HDMI-Pfad reduziert Adapterabhängigkeiten; das bekannte FHD-/QHD-16:9-Profil reicht für die Implementierung vollständig aus.
- **Nächste Aktion:** B-01 vor AP-06 durchführen: Tatsächliche CPU-/RAM-/SSD-/Windows-Konfiguration, Monitormodell, Skalierung, Bildwiederholrate, Kabel und Audioausgang erfassen sowie HDMI-Primärweg und USB-C-Ersatzweg im Netz-aus-Test prüfen.

## 8.2 ✅ OP-02: Lokaler Betrieb statt WLAN-Abhängigkeit
### tl;dr
- Geklärt: Server und Client lokal, Kabelanzeige verbindlich, Beobachterclient optional.

### Detail
Der Messefunk liegt außerhalb des kritischen Spielpfads. Ein drahtloser Watcher darf genutzt werden, wenn vor Ort eine geeignete Verbindung verfügbar ist; ohne diese bleibt die Kabelanzeige vollständig nutzbar.

### Empfehlung
- **Favorit:** Loopback als Standard, lokale passive Zweitanzeige über Monitorkabel; externen Watcherzugang nur bewusst aktivieren.
- **Begründung:** Das erfüllt Offlinebetrieb und spontane Funkoption ohne Netzabhängigkeit oder zusätzliche aktive Rolle.
- **Nächste Aktion:** AP-02 paketiert lokal; AP-06 prüft Funkverlust und Kabel-Hotplug.

## 8.3 ✅ OP-03: Medien und Gestaltung geklärt
### tl;dr
- Der Nutzer pitcht/verändert selbst und verwendet ausschließlich royaltyfree Quellen oder eigene Kreationen/Kompositionen.
- Ohne Nutzervorgabe zu Farbe und Schrift wird frei verwendbare Gestaltung gewählt.

### Detail
Die Mediengrundlage ist durch die Nutzerangabe geklärt. Es gibt keinen zusätzlichen kreativen Freigabeprozess und keine Pflicht zur Verwendung einer kostenpflichtigen Corporate-Schrift. Dieselbe Vorgabe gilt für die mit Leonardo AI erzeugten Werkzeugbilder; der Lizenzstatus wird pro Asset im Manifest dokumentiert (Kap. 3.4.6).

### Empfehlung
- **Favorit:** Gelieferte Eigen-/royaltyfree Medien übernehmen und notwendige eigene Ergänzungen einschließlich Schriften frei verwendbar auswählen.
- **Begründung:** Das setzt die klare Nutzervorgabe ohne zusätzliche Klärungsschleife um.
- **Nächste Aktion:** Reguläre Assetübernahme in AP-05; vorhandene Lizenzbedingungen/-hinweise bei der Paketierung berücksichtigen. Keine offene Konzeptentscheidung.

## 8.4 ✅ OP-04: Leads und Preisbenachrichtigung geklärt
### tl;dr
- Ziel: Leads erzeugen und mit potenziellen Kunden ins Gespräch kommen; Name und E-Mail erfassen.
- Ohne Kontaktdaten genügt ein Fantasiename; einen Preis nach der Messe gibt es nur mit erreichbarer E-Mail-Adresse.

### Detail
Der Verarbeitungszweck ist festgelegt, das E-Mail-Feld wird nicht deaktiviert. Kap. 3.8 enthält den DSGVO-Hinweis für Rangliste, Gewinnspiel und erlaubte Vertriebsansprache. Kreis-i, scrollbar bedienbares Overlay und Textänderung ohne Neubau bleiben verbindlich.

### Empfehlung
- **Favorit:** Name/Fantasiename und E-Mail im Ergebnisformular anbieten; Preisabwicklung an E-Mail, weitergehende Vertriebsansprache an separate freiwillige Einwilligung binden.
- **Begründung:** Unterstützt den Lead-Zweck und die Preisvergabe nach Messeende, während Spielen ohne Kontaktdaten möglich bleibt.
- **Nächste Aktion:** Formular und Hinweis in AP-05 umsetzen, Betreiberangaben und Termine bei der Einrichtung konfigurieren. Keine offene Zweck- oder Konzeptentscheidung; keine Behauptung einer bereits abgeschlossenen Implementierung oder rechtlichen Zertifizierung.

## 8.5 ✅ OP-05: Entfällt, bewusstes Überraschungsprojekt
### tl;dr
- Die bisherige Fach-/Marketingfreigabe entfällt ausdrücklich auf Nutzerentscheidung.

### Detail
Das Spiel ist ein „U-Boot“ und eine Überraschung. Retro-Romantik, Selbstironie und offensiver Humor sind gewollt, nicht bis zu einem Workshop vertagt. PKD-Bohrer/-Fräser mit Schaft sowie Sägen und Punkteabzüge sind als konkrete Spielidee beschrieben, nicht als Maschinensimulation.

### Empfehlung
- **Favorit:** Kein Workshop, keine zusätzliche Freigaberolle und kein OP-05-Gate im Umsetzungsplan.
- **Begründung:** Das entspricht ausdrücklich Ziel, Tonalität und Überraschungscharakter.
- **Nächste Aktion:** Keine Klärung erforderlich; technische Spielbarkeit wird regulär getestet.

## 8.6 ✅ OP-06: Technologieempfehlung
### tl;dr
- Die Entscheidung ist als Konzeptempfehlung getroffen, ihre Realisierungsdetails werden in AP-02 validiert.

### Detail
Blazor Server ist kein geeigneter primärer Echtzeit-Client; reines Phaser löst den lokalen Messebetrieb weniger vollständig. Option C trennt diese Verantwortungen.

### Empfehlung
- **Favorit:** Lokaler ASP.NET-Core-Server, Phaser/TypeScript-Client und Blazor-Shell auf demselben Windows-Convertible; Watcher lokal oder optional extern.
- **Begründung:** Direkte Touch-Interaktion, keine WLAN-Abhängigkeit und eine wiederverwendbare passive Beobachteransicht.
- **Nächste Aktion:** AP-02 validiert Start, Paketierung und Kioskmodus auf Zielhardware.

## 8.7 ✅ OP-07: Assetvertrag und Übernahmeregeln
### tl;dr
- Geliefert am 2026-10-11: 28 produktionsfertige WAV-Dateien unter `Assets/audio/laser/` und `Assets/audio/grind/` — 18 Werkstückstimmen (`cheer_high`, `yell_high`, `cheer_low`, `yell_low`) und 10 modusbezogene Effektsignale (`laser_*`, `grind_*`) gemäß Kap. 3.4.1, 3.4.2 und 3.4.4. Die Dateinamen sind endgültig und werden nicht umbenannt.
- Offen für B-02 sind `ui_confirm_*` (3), `feedback_fail_*` (3), `round_start_*` (3) und `round_end_*` (3), insgesamt 12 Dateien. `bgm.mp3` ist seit 2026-10-13 geliefert; Hör-/Loopfreigabe bleibt offen.
- Bildseite geklärt: Kap. 3.4.5 regelt Zonen, Größen und Zustände. Produktionsvorlage, Lieferliste mit 37 Assets, Zustands-Overlays, Requisiten, Störgegenstände, Leonardo-Ablauf und Sichtprüfung stehen in [00002](00002-c-vollmer-assetlieferung.concept.md); die Manifestgenerierung und die Autoren-Hilfstools stehen in [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md).

### Detail
Alle Aktionen und Zufallspools sind mit konkreten Dateinamen spezifiziert; die Zuordnung der gelieferten Dateien steht in Kap. 3.4.1 und 3.4.2. Die frühere Übernahmeplanung aus `yell01.mp3` bis `yell10.mp3` ist damit erledigt. Ein Klang-/Pegel-/Längen-Hörcheck der gelieferten Dateien steht noch aus; Korrekturen erfolgen allein über die Manifestzuordnung, nie über Dateinamen. Für die Werkzeugbilder sind Zonen, Skalierung und Zustände in Kap. 3.4.5 dieses Dokuments, Aufbau, Mengen und der Produktionsablauf mit Leonardo AI in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 und 3.7 festgelegt; die Trefferbewertung hängt allein an den normalisierten Zonen, nicht an Bilddetails. Das Manifest-Schema und ein vollständiges Beispiel stehen in Kap. 3.4.7 und Kap. 4 dieses Dokuments; die Datei selbst wird nach [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) erzeugt.

### Empfehlung
- **Favorit:** Die gelieferten 28 Dateien unverändert als endgültige Liefer-/Laufzeitdateien übernehmen; bis B-02 bleiben für die fehlenden 13 Dateien Platzhalter und die definierten Audio-Fallbacks zulässig. Ein Hörcheck korrigiert höchstens die Manifestzuordnung.
- **Begründung:** Der Nutzer weiß vorab, welche Dateien fehlen, während der Spielkern, Pools, Mixer und Fallbacks ohne Produktionsmedien implementierbar bleiben.
- **Nächste Aktion:** B-02 vor AP-06 abschließen: Hörcheck der 28 gelieferten Dateien (Zuordnung, Pegel, Länge, Clipping) durchführen; `ui_confirm_*` (3), `feedback_fail_*` (3), `round_start_*` (3), `round_end_*` (3) und `arcade_music_01.mp3` liefern oder produzieren; die Bildlieferliste aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 vollständig produzieren (zwölf Werkzeugbilder, zwölf Zustands-Overlays, Bühnenbild, vier Requisiten, sechs Verdecker, zwei Effektatlanten), Zonen über den Zonen-Editor setzen und das Manifest über die Werkzeugkette aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) generieren und validieren. AP-06 prüft Bild-/Zonencheck, Bühnenbild-/Ziellastcheck, Zuordnung, Pegel, Loopgrenzen und Mischer.

## 8.8 ✅ OP-08: Werkzeugbilder und Trefferzonen mit Leonardo AI
### tl;dr
- Werkzeugbilder werden mit Leonardo AI erzeugt und über einen festen Ablauf (uniformer Hintergrund → Freistellen → Normieren → Zonen setzen → Manifest) zu transparenten, konsistenten WebP-Dateien.
- Erfolg und Misserfolg hängen an den normalisierten Zonen `cut`/`shaft`, nicht an Bilddetails; Entscheidungsregeln stehen in Kap. 3.3.1, Zonen-, Größen- und Zustandsregeln in Kap. 3.4.5, Mengen und Werkzeugliste in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.

### Detail
Leonardo AI liefert Rasterbilder ohne verlässliche Transparenz; deshalb ist der Produktionsablauf verbindlich. Die Zone ist die einzige Autorität für einen Treffer; Bildvarianten (Größe, PKD) ändern nur `visualAssetId`, nie die Zonenlogik. Der Werkzeugumfang steht in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.1 (zwölf Werkzeuge); weitere Werkzeuge sind zusätzliche Manifest-Einträge ohne Codeumbau. Der im Werkzeug nicht vorkommende Hintergrund (bevorzugt Magenta `#FF00FF`) wird per Chroma-Key entfernt; bietet das gewählte Paket eine Freistellfunktion, ersetzt sie den Chroma-Key-Schritt. Freistellung und Normierung erfolgen in Affinity nach dem Ablauf in 00002 Kap. 3.7.

### Empfehlung
- **Favorit:** Werkzeugbilder nach Kap. 3.4.6 als transparente WebP mit festgehaltenem Seed erzeugen, Zonen über den Zonen-Editor aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) setzen und das Manifest generieren und validieren lassen.
- **Begründung:** Der Ablauf ist mit dem gewählten Generator stabil reproduzierbar und trennt Bildproduktion von der Trefferlogik; die Zonen bleiben auflösungsunabhängig und ohne Codeumbau erweiterbar.
- **Nächste Aktion:** Die Werkzeuge aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.1 in der dortigen Reihenfolge erzeugen, Silhouette bei 96 px prüfen, Zonen mit dem Zonen-Editor aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) setzen und die Manifestvalidierung im Prototyp nachweisen (AP-04, B-02).

## 8.9 ✅ OP-09: Showroom-Bühnenbild und freie Zielfläche
### tl;dr
- Geklärt: Das Spielbühnenbild ist die feste Showroom-Kulisse `bg_0001` mit orangefarbenem Boden, festen Maschinen und gelegentlich laufenden Kollegen in grauen Vollmer-Polos; die Ziele liegen davor und bleiben dadurch lesbar.
- Geklärt: Die gesamte Leinwand wird für einmalige Durchflüge verwendet; bei weniger als vier registrierten Zielen wird nachgefüllt. Es gibt keine Obergrenze, Überschneidungen werden nicht durch Flächen- oder Abstandslimits verhindert.

### Detail
Das Showroom-Bild wird mit Leonardo AI nach Kap. 3.4.6 dieses Dokuments erzeugt; Mengen und Ablage stehen in [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4.3, das konkrete Referenzbild liefert der Auftraggeber nach. Kollegen sind eigenständige `showroom-cast`-Requisiten und werden nicht in die Kulisse eingebrannt, damit sie animierbar und abschaltbar bleiben. Farbigkeit, Kontrast und Abdeckungsregeln stehen in Kap. 3.5; die technische Tiefenstaffelung steht in 00002 Kap. 3.6.

### Empfehlung
- **Favorit:** Kulisse `bg_0001` als 16:9-Master mit detaillarmer `stageArea`, Kollegen als separate Alpha-Requisiten und die Ziellastgrenzen aus Kap. 3.5 verwenden.
- **Begründung:** Das erzeugt die gewünschte leichte 2D-Tiefe einer ruhigen Kulisse, ohne Ziele zu verdecken oder die Lesbarkeit bei mehreren gleichzeitigen Zielen zu verlieren; neue Zieltypen brauchen nur einen Manifesteintrag.
- **Nächste Aktion:** Referenzbild des Auftraggebers in die Kulisse überführen, `stageArea` prüfen und im Manifest validieren; Nachweis in B-02, AP-04 und AP-06.
