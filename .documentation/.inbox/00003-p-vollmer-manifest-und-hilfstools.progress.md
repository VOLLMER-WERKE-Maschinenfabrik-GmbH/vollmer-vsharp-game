# Fortschritt: Vollmer Messe-Game: Manifest und Autoren-Hilfstools
| | |
|-|-|
| **Konzept:** | [Vollmer Messe-Game: Manifest und Autoren-Hilfstools](00003-c-vollmer-manifest-und-hilfstools.concept.md) |
| **Letzte Änderung:** | 2026-09-28 |

Arbeitspakete aus Kap. 5 des Konzepts. Diese Tabelle ist die verbindliche Arbeitsliste für die Umsetzung.
Status: `⬜ offen` / `🔄 in Arbeit` / `✅ erledigt` / `⛔ blockiert`

| AP | Beschreibung | Status | Erledigt am | Nachweis / Nächster Schritt |
|-|-|-|-|-|
| AP-01 | Schemas, Templates und ID-Register festlegen. | ⬜ offen | | Nächster Schritt: Schema in `VGame.Contracts` und `Assets/templates/` anlegen; gegen Kap. 4 des Hauptkonzepts gegenprüfen. |
| AP-02 | Build und Validierung implementieren. | ⬜ offen | | Nach AP-01: `vgtools manifest build` und `verify` mit Rundlauf-, Idempotenz- und Fehlerfalltests umsetzen. |
| AP-03 | Audio-Pool-Ableitung ergänzen. | ⬜ offen | | Nach AP-02: Poolvarianten aus `Assets/audio/**` ableiten, authored Datei ohne Dateilisten prüfen. |
| AP-04 | Zonen-Editor im Host implementieren. | ⬜ offen | | Nach AP-02: Werkzeugseite `/tools/zones` mit Abdeckungsprüfung und Tap-/Swipe-Vorhersage umsetzen. |
| AP-05 | Wizard, Normprüfer und Vorab-Erzeugung ergänzen. | ⬜ offen | | Nach AP-02: `new asset`, `manifest seed` aus der Lieferliste und `norm check` ohne Überschreiben bestehender Dateien implementieren. |
| AP-06 | CI-Anbindung und Beispielablauf dokumentieren. | ⬜ offen | | Nach AP-02 bis AP-05: Verify-Lauf im Repo verankern und Ablauf von der Datei bis zum Manifest dokumentieren. |
| AP-07 | Werkzeugkette in 00002 erproben. | ⬜ offen | | Nach AP-06 und AP-06 aus 00002: alle Assets aus 00002 über die Werkzeugkette aufnehmen und Doppelbuild prüfen. |

## Verbindliche Paketdefinitionen

Unverändert aus Kap. 5 des Konzepts; die obere Tabelle hält ausschließlich Bearbeitungsstatus und nächsten Schritt fest.

| AP | Ziel / Scope | Betroffene Komponenten | Abhängigkeiten | Validierung | Done-Kriterium |
|-|-|-|-|-|-|
| AP-01 | Schemas, Templates und ID-Register festlegen. | `VGame.Contracts`, `Assets/templates/`, `Assets/config/id-register.json` | Struktur aus 00002 AP-01 | Gegenprüfung gegen Kap. 4 des Hauptkonzepts und Kap. 3.3 | Schema, Templates und Register stimmen mit dem Vertrag überein; keine Schemaabweichung. |
| AP-02 | Build und Validierung implementieren. | `VGame.Tools.Manifest`, `VGame.Tools.Cli` | AP-01 | Rundlauf, Idempotenz, Duplikat-, Reservierungs- und Fehlendatei-Tests | `manifest build` erzeugt ein valides Manifest; ein zweiter Lauf erzeugt dieselbe Datei. |
| AP-03 | Audio-Pool-Ableitung ergänzen. | `VGame.Tools.Cli`, `Assets/config/audio.pools.json` | AP-02 | Poolbildung aus Dateinamen, leerer Pool mit Fallback, Poolgrößen 1/3/6/8 | Pools entstehen automatisch aus dem Ordner; authored Datei enthält keine Dateilisten. |
| AP-04 | Zonen-Editor im Host implementieren. | `VGame.Server` (Werkzeugseite), TypeScript-Oberfläche | AP-02 | Abdeckungsprüfung, Speichersperre bei Lücken, Vorhersage `tap`/`swipe`, Rundlauf der Koordinaten | Zonen und Pivot sind per Maus setzbar, die Abdeckung wird live geprüft und nur gültige Dateien gespeichert. |
| AP-05 | Wizard, Normprüfer und Vorab-Erzeugung ergänzen. | `VGame.Tools.Cli` | AP-02 | ID-Vergabe, Umbenennung, Nichtüberschreiben vorhandener Dateien, Normbefund, Seed-Lauf gegen die Lieferliste | Ein neues Bild erhält automatisch ID, Zielname und Metadatendatei; die Lieferliste wird ohne Handarbeit in Metadatendateien überführt, ohne bestehende Dateien zu verändern. |
| AP-06 | CI-Anbindung und Beispielablauf dokumentieren. | Skript im Repo, Kurzanleitung | AP-02, AP-03, AP-04, AP-05 | Prüflauf im Repo, Fehlerfall mit Exit-Code, Gegenlesen über `manifest.md` | `vgtools manifest verify` läuft im Repo und bricht bei Fehlern ab; die Kurzanleitung beschreibt den Ablauf von der Datei bis zum Manifest. |
| AP-07 | Werkzeugkette in 00002 erproben. | Assets aus [00002](00002-c-vollmer-assetlieferung.concept.md) | AP-06 und AP-06 aus 00002 | vollständiger Durchlauf über alle 37 Assets, Doppelbuild, Zonenabdeckung | Alle Assets aus 00002 sind über die Werkzeugkette aufgenommen; das Manifest ist valide und ohne Handarbeit entstanden. |

## Konzeptstand und Wiedereinstieg

- 2026-09-28: Konzept erstellt: Aufteilung generiert versus authored (Kap. 3.3) mit Beispielen der kleinen Autorendateien, Werkzeugkette mit sechs Bestandteilen (Kap. 3.4) einschließlich Vorab-Erzeugung `manifest seed` (Kap. 3.4, OP-06), Zonen-Editor als Werkzeugseite im Host (Kap. 3.5), Automatisierungsmatrix (Kap. 3.6), Aufwandsvergleich (Kap. 3.7), Projektanbindung unter `tools/` (Kap. 3.8), Schnittstellen (Kap. 4) und sieben Arbeitspakete. Kein Arbeitspaket abgeschlossen, keine Implementierung begonnen. Dokumentprüfung: `git diff --check` ohne Befund, Editor-Diagnostik ohne Fehler.
- Abhängigkeit: AP-01 setzt die Ablagestruktur aus 00002 Kap. 3.3 voraus; AP-07 setzt die Assets aus 00002 voraus.
- Exakter nächster Umsetzungsschritt: AP-01 auf `🔄 in Arbeit` setzen und Schema, Templates und ID-Register anlegen; alternativ zuerst 00002 AP-01 bis AP-03, damit reale Assets für AP-07 vorliegen.

Regeln:
- Vor der Bearbeitung das aktuelle Arbeitspaket auf `🔄 in Arbeit` setzen; gleichzeitig darf höchstens ein Arbeitspaket in Arbeit sein.
- `✅ erledigt` erst nach umgesetztem Code, erforderlicher Validierung und betroffenen Dokumentationsänderungen setzen. Den Test-/Build-Nachweis in der letzten Spalte festhalten.
- Bei `⛔ blockiert` Ursache, benötigte Entscheidung oder fehlende Voraussetzung sowie den exakten Wiedereinstieg in der letzten Spalte festhalten.
- Bei Unterbrechung bleibt das aktuelle Arbeitspaket `🔄 in Arbeit`; die letzte Spalte beschreibt den konkreten nächsten Schritt.
