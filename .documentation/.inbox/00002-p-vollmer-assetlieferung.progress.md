# Fortschritt: Vollmer Messe-Game: Asset- und Bildlieferung
| | |
|-|-|
| **Konzept:** | [Vollmer Messe-Game: Asset- und Bildlieferung](00002-c-vollmer-assetlieferung.concept.md) |
| **Letzte Änderung:** | 2026-09-28 |

Arbeitspakete aus Kap. 5 des Konzepts. Diese Tabelle ist die verbindliche Arbeitsliste für die Umsetzung.
Status: `⬜ offen` / `🔄 in Arbeit` / `✅ erledigt` / `⛔ blockiert`

| AP | Beschreibung | Status | Erledigt am | Nachweis / Nächster Schritt |
|-|-|-|-|-|
| AP-01 | Ablage, Vorlagen und ID-Register anlegen. | ⬜ offen | | Nächster Schritt: Repo-Struktur aus Kap. 3.3, `media-source/`, `Assets/templates/` und `id-register.json` anlegen. |
| AP-02 | Bühne, Requisiten und Verdecker produzieren. | ⬜ offen | | Nach AP-01: `bg_0001`, `cast_0001`–`cast_0004`, `occ_0001`–`occ_0006` normiert in `Assets/` ablegen. |
| AP-03 | Zwölf Werkzeugbilder produzieren. | ⬜ offen | | Nach AP-01: `tool_0001`–`tool_0012` nach Kap. 3.4.1 erzeugen, freistellen und normieren. |
| AP-04 | Zwölf Zustands-Overlays produzieren. | ⬜ offen | | Nach AP-03: `ovl_0001`–`ovl_0012` aus den Werkzeugformen ableiten und Deckung prüfen. |
| AP-05 | Effektatlanten bereitstellen und zuordnen. | ⬜ offen | | Nach AP-01: `fx_0001` und `fx_0002` liefern, Erfolg/Fehler nur über Tint und Intensität. |
| AP-06 | Zonen und Zustände in Sidecars eintragen und Manifest bauen. | ⬜ offen | | Nach AP-02 bis AP-05 und AP-06 aus 00003: Sidecars füllen, Manifest generieren, Zonenabdeckung und `shaft`-Vorrang prüfen. |
| AP-07 | Asset-Abnahme für B-02 des Hauptkonzepts durchführen. | ⬜ offen | | Nach AP-06: Checkliste Kap. 3.7.1, Tiefenprofil und Ziellast auf der Zielhardware bestätigen. |

## Verbindliche Paketdefinitionen

Unverändert aus Kap. 5 des Konzepts; die obere Tabelle hält ausschließlich Bearbeitungsstatus und nächsten Schritt fest.

| AP | Ziel / Scope | Betroffene Komponenten | Abhängigkeiten | Validierung | Done-Kriterium |
|-|-|-|-|-|-|
| AP-01 | Ablage, Vorlagen und ID-Register anlegen. | Repo-Struktur Kap. 3.3, `media-source/`, `Assets/templates/`, `id-register.json` | — | Ordner- und Vorlagencheck gegen Kap. 3.3/3.8 | Alle Pfade und Vorlagen existieren, das ID-Register enthält die Start-IDs, kein Asset liegt am falschen Ort. |
| AP-02 | Bühne, Requisiten und Verdecker produzieren. | `bg_0001`, `cast_0001`–`cast_0004`, `occ_0001`–`occ_0006` | AP-01 | Checkliste Kap. 3.7.1 für Kulisse und Requisiten; `stageArea`- und Detaillarmutcheck | Elf Assets liegen normiert in `Assets/`, die Kulisse enthält keine Personen und eine detaillfreie `stageArea`. |
| AP-03 | Zwölf Werkzeugbilder produzieren. | `tool_0001`–`tool_0012` | AP-01 | Silhouette bei 96 px, Alpha-Kante auf orangefarbenem Grund, ein Werkzeug pro Bild | Zwölf zustandsneutrale Werkzeugbilder liegen normiert vor, PKD ist nur an der Schneide sichtbar. |
| AP-04 | Zwölf Zustands-Overlays produzieren. | `ovl_0001`–`ovl_0012` | AP-03 | Deckungsgleichheit auf der `cut`-Zone in allen drei Größenklassen | Alle vier Zustände sind je Familie sichtbar unterscheidbar und sitzen in allen Größenklassen deckungsgleich. |
| AP-05 | Effektatlanten bereitstellen und zuordnen. | `fx_0001`, `fx_0002`, Erfolgs-/Fehler-Tint | AP-01 | Sichtprüfung Erfolg gegen Fehler ausschließlich über Tint und Intensität | Zwei geteilte Atlanten sind vorhanden, Erfolg und Fehler sind ohne eigene Atlanten unterscheidbar. |
| AP-06 | Zonen und Zustände in Sidecars eintragen und Manifest bauen. | `<assetId>.zones.json`, `<assetId>.meta.json`, Werkzeugkette aus 00003 | AP-02, AP-03, AP-04, AP-05, AP-06 aus 00003 | Zonenabdeckung lückenlos, `shaft`-Vorrang, Manifest validiert | Das generierte Manifest enthält alle Assets dieses Konzepts, ist valide und ohne Handarbeit erzeugt. |
| AP-07 | Asset-Abnahme für B-02 des Hauptkonzepts durchführen. | Checkliste Kap. 3.7.1, Zonen-/Tiefenprofil-/Ziellastcheck | AP-06 | Vollständige Sicht-, Zonen- und Ziellastprüfung plus Stichprobe auf der Zielhardware | Die Abnahmeliste ist vollständig abgehakt, Tiefenprofil und Ziellastgrenzen sind auf der Zielhardware bestätigt. |

## Konzeptstand und Wiedereinstieg

- 2026-09-28: Asset- und Bildlieferkonzept erstellt: 37 Assets mit IDs und Mengen (Kap. 3.4), Zustandsmodell `dull`/`in_progress`/`sharp`/`failed` (Kap. 3.5), Tiefenverfahren mit Profilwerten (Kap. 3.6), Repo- und Solution-Struktur (Kap. 3.3), Produktionsablauf mit Affinity-Normierung (Kap. 3.7), Autorendateien (Kap. 4) und sieben Arbeitspakete. Kein Arbeitspaket abgeschlossen, keine Produktion begonnen. Dokumentprüfung: `git diff --check` ohne Befund, Editor-Diagnostik ohne Fehler.
- Zusätzlich erforderlich im Hauptkonzept 00001: Zustandsregeln für `failed` in Kap. 3.3/3.3.1, Verweis der Bildlieferliste auf dieses Konzept, Anpassung von B-02 sowie der offenen Punkte OP-07 bis OP-09.
- Exakter nächster Umsetzungsschritt: AP-01 auf `🔄 in Arbeit` setzen und die Ablage nach Kap. 3.3 anlegen; anschließend AP-02 (Bühne und Requisiten) und AP-03 (Werkzeugbilder).

Regeln:
- Vor der Bearbeitung das aktuelle Arbeitspaket auf `🔄 in Arbeit` setzen; gleichzeitig darf höchstens ein Arbeitspaket in Arbeit sein.
- `✅ erledigt` erst nach umgesetztem Code, erforderlicher Validierung und betroffenen Dokumentationsänderungen setzen. Den Test-/Build-Nachweis in der letzten Spalte festhalten.
- Bei `⛔ blockiert` Ursache, benötigte Entscheidung oder fehlende Voraussetzung sowie den exakten Wiedereinstieg in der letzten Spalte festhalten.
- Bei Unterbrechung bleibt das aktuelle Arbeitspaket `🔄 in Arbeit`; die letzte Spalte beschreibt den konkreten nächsten Schritt.
