# Vollmer Messe-Game: Manifest und Autoren-Hilfstools

| | | |
|-|-|-|
| **Erstelldatum:** | 2026-09-28 |
| **Letzte Änderung:** | 2026-10-11 |
| **Issue:** | 00003 |

## Commit-Vorschlag für den finalen Gesamt-Commit

> **Status:** `vorläufig` — Der Vorschlag wird nach Umsetzung, Einarbeitung aller Code-Review-Findings und erfolgreicher finaler Review anhand der tatsächlich enthaltenen Änderungen aktualisiert. Es wird kein separater Konzept-Commit erstellt.

```text
[00003] Manifestgenerierung und Autoren-Hilfstools bereitstellen

- Geplante Umsetzung: Manifest-Build-CLI, Zonen-Editor, ID-Register, Normprüfer und Audio-Pool-Builder als Werkzeugkette unter `tools/`.
- Geplante Entscheidung: Das Manifest wird generiert; von Hand pflegt der Autor nur wenige kleine Eingabedateien und die Zonen per Klick.
- Geplante Absicherung: Rundlauf-, Idempotenz-, Duplikat-, Reservierungs- und Nichtüberschreib-Tests sowie ein CI-Prüflauf.
- Risiken und offene Punkte: keine blockierenden; Hauptrisiko ist eine trotzdem von Hand geänderte Manifestdatei.
```

# 0. Entscheidungsvorlage

| | |
|-|-|
| **Problem** | Die Manifestpflege von Hand ist zu aufwendig und fehleranfällig für den verfügbaren Zeitrahmen. |
| **Lösung** | Eine Werkzeugkette erzeugt das Manifest aus Bilddateien, kleinen Eingabedateien und per Maus gesetzten Zonen. |
| **Entscheidungsbedarf** | Freigabe der Aufteilung „generiert versus authored“ und des Web-Zonen-Editors im lokalen Host. |
| **Top-Risiko** | Eine trotzdem von Hand editierte `manifest.json` wird beim nächsten Build still überschrieben. |
| **Blockierende offene Punkte** | — entfällt — (OP-01 bis OP-05 sind geklärt). |

# 1. Kontext, Zielsetzung & Use Case

## 1.1 tl;dr
- Das Manifest bleibt als Laufzeitvertrag bestehen, wird aber **vollständig generiert** statt von Hand geschrieben.
- Von Hand entstehen nur vier kleine Eingabedateien plus die per Klick gesetzten Zonen je Asset.
- Fünf Werkzeuge decken den Bedarf: Manifest-Build, Vorab-Erzeugung aus der Lieferliste, Zonen-Editor, Wizard mit ID-Register, Normprüfer und Audio-Pool-Builder.
- Die Manifesteinträge aller 37 Assets aus 00002 können **vorab** erzeugt werden; die spätere Lieferung besteht nur noch aus Bilddateien im richtigen Ordner.
- Die Manifestgenerierung läuft zur Entwicklungs- und Paketierungszeit, **nie** am Messestand (Neu-Z-07 des Hauptkonzepts).

## 1.2 Ziele und Nicht-Ziele

| Ziel | Beschreibung |
|-|-|
| Z-01 | Kein Mensch schreibt Trefferzonen, Dateiverweise, IDs oder Audiopools von Hand in das Manifest. |
| Z-02 | Das Manifest entsteht reproduzierbar: zweimal bauen ergibt dieselbe Datei. |
| Z-03 | Fehlerhafte, fehlende oder doppelte Assets verhindern den Build statt still durchzurutschen. |
| Z-04 | Zonen werden per Klick auf dem Bild gesetzt und mit sichtbarer Abdeckungsprüfung bestätigt. |
| Z-05 | Die Werkzeuge ändern das Manifest-Schema aus Kap. 4 des Hauptkonzepts nicht. |
| Z-06 | Der Messestand braucht weder Werkzeug noch Build; er konsumiert nur `Assets/config/manifest.json`. |

| Nicht-Ziel (Scope) | Begründung |
|-|-|
| NZ-01 | Keine Laufzeit-Editorfunktion am Messestand. | Am Stand gilt ausschließlich geprüfte Konfiguration (Neu-Z-07 des Hauptkonzepts). |
| NZ-02 | Keine Generative-Füllung von Spielregeln oder Punkten. | Punkte, Toleranzen und Dauern bleiben fachliche Konfiguration. |
| NZ-03 | Kein Ersatz für die Bildproduktion. | Freistellen und Normieren bleiben bei Affinity und dem Auftraggeber ([00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.7). |
| NZ-04 | Keine eigene Datenbank oder ein Serverdienst für die Autorenwerkzeuge. | Dateien im Repository sind ausreichend, nachvollziehbar und versionierbar. |
| NZ-05 | Keine automatische Klang- oder Lautheitsfreigabe. | Der Hörcheck bleibt ein menschlicher Schritt. |

## 1.3 Auslöser

Auslöser ist die Rückmeldung zum Hauptkonzept [00001](00001-c-vollmer-messe-game.concept.md): Ein Manifest mit Trefferzonen, Audiopools, Lizenzstatus und Spielwerten je Asset ist in Handarbeit nicht in vertretbarer Zeit zu pflegen. Der Auftraggeber kann Rohmaterial liefern und normierte Bilder erstellen; Zonen, IDs und Zuordnungen sollen jedoch automatisch entstehen und bereits vor der Bildlieferung vorbereitet sein.

# 2. Analyse / Ist-Zustand

## 2.1 tl;dr
- Kap. 3.4.7 des Hauptkonzepts zeigt ein handgeschriebenes Manifest; bei 37 Assets wären das mehrere hundert manuell gepflegte Felder.
- Ein großer Teil dieser Felder ist ableitbar: Datei, Kategorie, ID, Format, Größe, Prüfsumme und Audiopool folgen aus Ablage und Benennung.
- Wirklich authored sind nur wenige Werte: Trefferzonen, Pivot, Lizenz-/Herkunftsangaben und die Spielwerte.

## 2.2 Ableitbar oder authored

| Feldgruppe | Herkunft | Aufwand von Hand |
|-|-|-|
| Datei, Pfad, Format, Pixelmaße, Dateigröße, Prüfsumme | ableitbar aus dem Dateisystem | entfällt vollständig |
| ID, Kategorie | ableitbar aus Ordner, Dateiname und ID-Register | entfällt vollständig |
| Audio-Dateigruppen (`cheer_high`, `laser` usw.) und ihre Poolzuordnung | Dateigruppen ableitbar aus `<aktion>_<nn>.<format>` im Audioordner; Poolzuordnung authored in `audio.pools.json` | klein, eine Zeile je Pool |
| Trefferzonen `cut`/`shaft`, Pivot | authored, per Maus gesetzt | klein, aber präzise — dafür ein Editor |
| `toolType`, `interaction`, `sizeClass`, `pkd`, `states[]` | authored, wenige Werte je Asset | klein, per Wizard |
| `licenseStatus`, Quelle, Generator, Modell, Seed, Prompt | authored beziehungsweise aus dem Wizard | klein, einmalig |
| Punkte, Dauer, Toleranzen, Tiefenprofil, Mischer | authored, **einmal global** statt je Asset | entfällt je Asset |

**Kernproblem:** Die heutige Beschreibung legt alle Felder je Asset ab, obwohl die meisten global oder ableitbar sind. Dadurch wächst die Handarbeit linear mit der Assetzahl, obwohl nur zwei Feldgruppen tatsächlich pro Asset gepflegt werden müssen.

# 3. Lösungskonzept / Soll-Zustand

## 3.1 tl;dr
- Trennung in **generiertes Manifest** (Laufzeitvertrag) und vier kleine **Eingabedateien**.
- Fünf Werkzeuge unter `tools/`: Build-CLI, Zonen-Editor, Wizard mit ID-Register, Normprüfer, Audio-Pool-Builder.
- Der Zonen-Editor läuft als Werkzeugseite im vorhandenen lokalen Host und schreibt `<assetId>.zones.json`.
- Einträge können **vorab** aus der Lieferliste erzeugt werden (`manifest seed`), damit nach der Bildlieferung nichts mehr eingetragen werden muss.
- Der Build läuft in der Entwicklung beziehungsweise beim Paketieren und ist in der CI prüfbar.

## 3.2 Nicht-Ziele

| Nicht-Ziel (Implementierung) | Begründung |
|-|-|
| NZ-01 | Keine Schemaänderung am Manifest aus Kap. 4 des Hauptkonzepts. | Der Vertrag bleibt stabil; nur der Erzeuger ändert sich. |
| NZ-02 | Keine zweite Wahrheit über Spielwerte je Asset. | Punkte und Toleranzen stehen global in `gameplay.json`. |
| NZ-03 | Keine Bildbearbeitung im Zonen-Editor. | Der Editor liest Bilder nur, er verändert sie nicht. |
| NZ-04 | Keine stillen Standardwerte für entscheidungsrelevante Felder. | Fehlende Pflichtfelder brechen den Build ab. |

## 3.3 Aufteilung: generiert versus authored

| Datei | Art | Pflege durch | Inhalt |
|-|-|-|-|
| `Assets/config/manifest.json` | generiert | Werkzeugkette | vollständiges Laufzeitmanifest (Schema Kap. 4 des Hauptkonzepts) |
| `Assets/config/manifest.md` | generiert | Werkzeugkette | lesbare Revision aller Einträge zum Gegenlesen und für Reviews |
| `Assets/config/id-register.json` | generiert | Werkzeugkette | vergebene und reservierte IDs je Kategorie |
| `Assets/config/gameplay.json` | authored | Mensch | Punkte, Rundendauer, Toleranzen, Ziellastgrenzen, Zustandsdauern |
| `Assets/config/audio.pools.json` | authored | Mensch | Pools: ID, Dateigruppe, Kategorie, Lautstärkeprofil, Fallback, Beutelregel — **ohne** Dateiliste |
| `Assets/config/depth.profiles.json` | authored | Mensch | Tiefenstaffelung ([00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.6) |
| `media-source/zone-authoring/<assetId>.zones.json` | authored (Editor) | Editor | Pivot und Trefferzonen je Werkzeug |
| `media-source/assets/<assetId>.meta.json` | authored (Wizard) | Wizard | Kategorie, Werkzeugtyp, Größenklasse, PKD, Zustände, Herkunft, Lizenz, Seed, Prompt |
| `media-source/plan/delivery-list.csv` | einmaliger Import | Mensch, KI-gestützt | Mengengerüst aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 als Eingabe für die Vorab-Erzeugung |
| `Assets/templates/<kategorie>.template.json` | authored | Mensch | Pflichtfelder und Grenzwerte je Kategorie für die Validierung |

Die Dateiliste der Audiopools entsteht **nicht** von Hand: Der Build liest den Audioordner, gruppiert nach `<aktion>_<nn>.<format>` und füllt `variants[]` aus den vorhandenen Dateien. Die authored Datei beschreibt das Verhalten des Pools und nennt seine Dateigruppe (`fileGroup`, etwa `cheer_high` für `laser_success`); eigene, sprechende Dateinamen bleiben damit ohne Umbenennung gültig.

**Größenordnung der authored Dateien (Beispiel, nicht vollständig):**

```json
// Assets/config/audio.pools.json — Pools nennen ihre Dateigruppe; variants[] entsteht aus dem Ordner
{
  "pools": [
    { "poolId": "laser_success", "fileGroup": "cheer_high", "category": "laser-sfx", "volumeProfile": "voice",
      "bagRule": "shuffle_no_repeat", "fallback": "laser_effect" },
    { "poolId": "saw_fail", "fileGroup": "yell_low", "category": "grind-sfx", "volumeProfile": "voice",
      "bagRule": "shuffle_no_repeat", "fallback": "grind_effect" }
  ]
}
```

```json
// media-source/zone-authoring/tool_0001.zones.json — vom Editor geschrieben
{
  "assetId": "tool_0001",
  "canvas": { "width": 1024, "height": 1024 },
  "pivot": { "x": 0.5, "y": 0.5 },
  "hitZones": [
    { "zoneId": "cut", "role": "cut", "points": [[0.30, 0.05], [0.70, 0.10], [0.66, 0.38], [0.34, 0.34]] },
    { "zoneId": "shaft", "role": "shaft", "points": [[0.34, 0.05], [0.66, 0.10], [0.60, 0.95], [0.40, 0.95]] }
  ]
}
```

```json
// media-source/assets/tool_0001.meta.json — vom Wizard geschrieben, rund zehn Felder
{
  "assetId": "tool_0001", "category": "tool-image", "toolType": "drill", "interaction": "tap",
  "sizeClass": "small", "pkd": false, "states": ["dull", "in_progress", "sharp", "failed"],
  "licenseStatus": "royaltyfree", "source": "leonardo-ai", "model": "<modell>", "seed": 123456,
  "prompt": "<prompt-id>", "createdUtc": "2026-09-28T10:00:00Z"
}
```

Damit bleibt pro Asset nur diese eine kleine Datei plus die Zonendatei — Trefferzonen, IDs, Dateiverweise, Prüfsummen und Poolvarianten entstehen im Build.

## 3.4 Werkzeugkette

| Werkzeug | Aufgabe | Eingabe | Ausgabe |
|-|-|-|-|
| `vgtools manifest build` | Manifest erzeugen und validieren | `Assets/`, `media-source/`, Templates | `manifest.json`, `manifest.md`, `id-register.json` |
| `vgtools manifest seed` | **Vorab-Erzeugung** aller Manifesteinträge aus der Lieferliste | `media-source/plan/delivery-list.csv` | `<assetId>.meta.json` je geplantem Asset, Einträge im ID-Register; **keine** Bilddateien |
| `vgtools manifest verify` | Prüflauf ohne Schreiben (CI) | wie oben | Exit-Code 0/1 und Befundliste |
| `vgtools new asset` | Wizard für ein neues Bildasset | Bilddatei im Eingangsordner | `<assetId>.meta.json`, Zielname, ID-Eintrag |
| `vgtools norm check` | Normprüfer für gelieferte Bilder | Bilddatei | Befund: Größe, Rand, Alpha-Kante, Silhouettenmaß |
| `vgtools audio pools build` | Poolvarianten aus Dateigruppen ableiten | `Assets/audio/**` | Ergänzung in `manifest.json` |
| Zonen-Editor `/tools/zones` | Zonen und Pivot per Maus setzen | Laufzeitbild | `<assetId>.zones.json` |

**Wichtige Reihenfolge:** Der Editor und der Wizard erzeugen ausschließlich Eingabedateien. Nur der Build schreibt in `Assets/config/`. Dadurch ist jederzeit nachvollziehbar, welche Datei Quelle und welche Datei Ergebnis ist.

**Vorab-Erzeugung:** Sobald die Bildlieferliste steht, muss niemand später Einträge nachtragen. `vgtools manifest seed` legt für jedes geplante Asset seine Metadatendatei an und reserviert die ID. Erzeugte Bilder werden danach nur noch in `Assets/` gelegt und per Build zugeordnet. Solange eine Bilddatei fehlt, meldet der Build den Eintrag als `planned`; `vgtools manifest verify` bleibt dabei rot, `manifest build` liefert aber bereits eine vollständige, gegenlesbare Aufstellung.

## 3.5 Zonen-Editor

- Läuft als lokale Werkzeugseite des ASP.NET-Core-Hosts (`/tools/zones`), also ohne zusätzliche Laufzeitumgebung und mit demselben Startablauf wie das Spiel.
- Lädt ein Werkzeugbild samt Zoom, setzt Punkte für `cut` und `shaft`, verschiebt den Pivot und zeigt die Abdeckung unmittelbar an.
- **Abdeckungsprüfung live:** nicht abgedeckte opake Pixel werden rot markiert; eine Lücke verhindert das Speichern.
- **Vorhersage live:** wählbarer Modus `tap`/`swipe` simuliert mit der Konfiguration aus `gameplay.json`, welche Zone an einer Testposition gewinnt; so ist der `shaft`-Vorrang an der gemeinsamen Grenze sofort sichtbar.
- Speichert ausschließlich `<assetId>.zones.json`; die Manifestdatei wird nicht angefasst.
- Setzt den Knoten im Normalmaß `0..1` relativ zur Werkzeug-Bounding-Box, damit die Zonen mit jeder Größenklasse skalieren.

## 3.6 Was Künstliche Intelligenz erzeugt

| Aufgabe | Wer | Werkzeug / Ansatz | Ergebnis |
|-|-|-|-|
| Code und Parser (Manifest lesen/schreiben, Zonen als Polygone rendern, Audio-Beutel steuern) | KI, stark | Generierung aus diesem Konzept plus Kap. 4 des Hauptkonzepts | C#-Bibliothek in `VGame.Contracts`, TypeScript-Leser im Client |
| Basisbilder und SFX als Rohmaterial | KI, stark | Leonardo AI für Bilder, Audio-KI für Sounds, danach Affinity und Pitch durch den Auftraggeber | Master-Dateien in `media-source/` |
| Zonen und Pivots | Werkzeug plus Mensch | Zonen-Editor aus Kap. 3.5 | `<assetId>.zones.json` |
| IDs, Dateiverweise, Prüfsummen, Audiopools, Revision | Werkzeugkette | Build aus Kap. 3.4 | `manifest.json`, `manifest.md`, `id-register.json` |
| Spielwerte, Poolverhalten, Tiefenprofil, Templates | Mensch | kleine Eingabedateien | vier authoring-Dateien |

**Grenze:** KI liefert Code und Rohmaterial, aber niemals die Laufzeitquelle. Das Manifest entsteht ausschließlich durch den Build aus Dateien und authored Werten.

## 3.7 Aufwand vorher und nachher

| Schritt | Vorher (handgeschrieben) | Nachher (Werkzeugkette) |
|-|-|-|
| 12 Werkzeugbilder eintragen (je rund 15 Felder) | rund 180 Felder von Hand | 12 Wizard-Durchläufe mit je drei Angaben |
| Trefferzonen | von Hand als Zahlenpaare | per Klick, mit Abdeckungsprüfung |
| Audiopools und Varianten | Dateilisten von Hand pflegen | automatisch aus dem Ordner |
| IDs und Reservierung | manuell, kollisionsgefährdet | Register automatisch |
| Prüfsummen, Maße, Formate | nicht vorgesehen | automatisch |
| Prüfung vor der Runde | Sichtprüfung | `vgtools manifest verify` in der CI |

## 3.8 Anbindung an die Solution

Die Werkzeugkette liegt unter `tools/` in der Struktur aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.3:

```text
tools/
  VGame.Tools.Cli/            vgtools: build, verify, new asset, norm check, audio pools
  VGame.Tools.Manifest/       Bibliothek: Scannen, Ableiten, Validieren, Schreiben
  VGame.Tools.Manifest.Tests/ MSTest für Rundlauf, Idempotenz und Fehlerfälle
```

Der Zonen-Editor ist keine eigene Anwendung, sondern eine Entwicklungsseite des Hosts aus `src/VGame.Server/`. Damit gibt es genau eine ausführbare Anwendung und keine zweite Werkzeugoberfläche, die gepflegt werden müsste.

# 4. Contracts

## 4.1 tl;dr
- Die Kommandozeile hat feste Rückgabecodes und schreibt nur in `Assets/config/`.
- Authorene Dateien werden nie überschrieben; sie werden nur ergänzt.
- Der Editor spricht ausschließlich mit dem lokalen Host.

| Schnittstelle | Richtung | Inhalt |
|-|-|-|
| `vgtools manifest build --assets Assets --source media-source --out Assets/config/manifest.json` | Entwickler -> Dateien | erzeugt `manifest.json`, `manifest.md`, `id-register.json`; Exit `0` bei Erfolg, `2` bei Validierungsfehler, `3` bei fehlender Datei |
| `vgtools manifest seed --plan media-source/plan/delivery-list.csv` | Entwickler -> Dateien | legt fehlende `<assetId>.meta.json` an und reserviert IDs; Exit `0` bei Erfolg, `4` bei Kollision mit vorhandenen IDs; überschreibt nie eine vorhandene Metadatendatei |
| `vgtools manifest verify` | CI -> Dateien | kein Schreiben; Exit `0`/`2`/`3` mit Befundliste |
| `vgtools new asset --file <pfad>` | Entwickler -> Dateien | legt `<assetId>.meta.json` an, vergibt die nächste freie ID, benennt die Datei normgerecht um |
| `vgtools norm check --file <pfad>` | Entwickler -> Report | Befund zu Größe, Rand, Alpha-Kante und Silhouette; keine Dateiänderung |
| `GET /tools/zones` | Browser -> Host | Editorseite mit Assetauswahl |
| `GET /tools/zones/api/asset/{id}` | Editor -> Host | Bildpfad, bestehende Zonen, Kategorie, Größenklassen |
| `POST /tools/zones/api/asset/{id}` | Editor -> Host | schreibt `<assetId>.zones.json`; lehnt Lücken und ungültige Koordinaten ab |
| `GET /tools/zones/api/preview?mode=tap\|swipe&x=&y=` | Editor -> Host | simuliertes Ergebnis nach Kap. 3.3.1 des Hauptkonzepts |

**Regeln:** Der Build liest Templates aus `Assets/templates/` und erzwingt die Pflichtfelder je Kategorie. Fehlende Pflichtfelder, doppelte IDs, unbekannte Kategorien und Dateien ohne ID-Eintrag brechen ab. Der Build schreibt niemals in `media-source/` und niemals in `Assets/images/`. Die Werkzeugseite ist nur lokal gebunden und nicht Teil des Messestands.

# 5. Implementierungsplan

## 5.1 tl;dr
- Sieben Arbeitspakete: erst Schemas und Register, dann Build und Validierung, danach die Bedienwerkzeuge.
- Die Reihenfolge ist so gewählt, dass nach AP-03 bereits ein nutzbares Manifest existiert.
- Als Bedienform ist die Werkzeugseite im lokalen Host der Favorit.

| AP | Ziel / Scope | Betroffene Komponenten | Abhängigkeiten | Validierung | Done-Kriterium |
|-|-|-|-|-|-|
| AP-01 | Schemas, Templates und ID-Register festlegen. | `VGame.Contracts`, `Assets/templates/`, `Assets/config/id-register.json` | Struktur aus 00002 AP-01 | Gegenprüfung gegen Kap. 4 des Hauptkonzepts und Kap. 3.3 | Schema, Templates und Register stimmen mit dem Vertrag überein; keine Schemaabweichung. |
| AP-02 | Build und Validierung implementieren. | `VGame.Tools.Manifest`, `VGame.Tools.Cli` | AP-01 | Rundlauf, Idempotenz, Duplikat-, Reservierungs- und Fehlendatei-Tests | `manifest build` erzeugt ein valides Manifest; ein zweiter Lauf erzeugt dieselbe Datei. |
| AP-03 | Audio-Pool-Ableitung ergänzen. | `VGame.Tools.Cli`, `Assets/config/audio.pools.json` | AP-02 | Poolbildung aus Dateinamen, leerer Pool mit Fallback, Poolgrößen 1/3/6/8 | Pools entstehen automatisch aus dem Ordner; authored Datei enthält keine Dateilisten. |
| AP-04 | Zonen-Editor im Host implementieren. | `VGame.Server` (Werkzeugseite), TypeScript-Oberfläche | AP-02 | Abdeckungsprüfung, Speichersperre bei Lücken, Vorhersage `tap`/`swipe`, Rundlauf der Koordinaten | Zonen und Pivot sind per Maus setzbar, die Abdeckung wird live geprüft und nur gültige Dateien gespeichert. |
| AP-05 | Wizard, Normprüfer und Vorab-Erzeugung ergänzen. | `VGame.Tools.Cli` | AP-02 | ID-Vergabe, Umbenennung, Nichtüberschreiben vorhandener Dateien, Normbefund, Seed-Lauf gegen die Lieferliste | Ein neues Bild erhält automatisch ID, Zielname und Metadatendatei; die Lieferliste wird ohne Handarbeit in Metadatendateien überführt, ohne bestehende Dateien zu verändern. |
| AP-06 | CI-Anbindung und Beispielablauf dokumentieren. | Skript im Repo, Kurzanleitung | AP-02, AP-03, AP-04, AP-05 | Prüflauf im Repo, Fehlerfall mit Exit-Code, Gegenlesen über `manifest.md` | `vgtools manifest verify` läuft im Repo und bricht bei Fehlern ab; die Kurzanleitung beschreibt den Ablauf von der Datei bis zum Manifest. |
| AP-07 | Werkzeugkette in 00002 erproben. | Assets aus [00002](00002-c-vollmer-assetlieferung.concept.md) | AP-06 und AP-06 aus 00002 | vollständiger Durchlauf über alle 37 Assets, Doppelbuild, Zonenabdeckung | Alle Assets aus 00002 sind über die Werkzeugkette aufgenommen; das Manifest ist valide und ohne Handarbeit entstanden. |

| Option | Vorteile | Nachteile |
|-|-|-|
| A: Nur CLI, Zonen als JSON von Hand | kleinster Bauaufwand, keine Oberfläche | Zonen von Hand sind genau der mühsame Schritt, der vermieden werden soll |
| B: CLI plus Desktop-Editor (WPF) | gewohnte Windows-Anwendung, große Zeichenfläche | zweite Anwendung mit eigener Start-, Build- und Paketpflege |
| C: CLI plus Werkzeugseite im lokalen Host | kein neues Laufzeitmodell, gleicher Startablauf, Mausbedienung und Vorhersage direkt über die Spiellogik | Host muss um eine reine Entwicklungsoberfläche erweitert werden |

**Favorit:** Option C.

**Begründung:** Die Vorhersage „welche Zone gewinnt“ entsteht in derselben Laufzeit, die später wertet; damit gibt es keine zweite, abweichende Zonenauswertung. Gegenüber B entfällt ein zweiter Anwendungslebenszyklus, gegenüber A entfällt die manuelle Zahlenarbeit.

# 6. Tests

## 6.1 tl;dr
- Im Mittelpunkt stehen Rundlauf, Idempotenz, Fehlererkennung und Nichtüberschreiben.
- Die Zonenprüfung deckt Abdeckung, Grenzvorrang und Skalierung ab.
- Die Werkzeugkette wird an den echten Assets aus 00002 geprobt.

| Bereich | Prüffall / Akzeptanzkriterium |
|-|-|
| Rundlauf | Zweimaliger Build auf unverändertem Bestand ergibt dieselbe `manifest.json` (byte-identisch, stabile Schlüsselreihenfolge). |
| Idempotenz | Wiederholtes `new asset` für dieselbe Datei erzeugt keine zweite ID und überschreibt keine vorhandene Metadatendatei; ein zweiter Seed-Lauf erzeugt keine Duplikate. |
| Vorab-Erzeugung | Nach dem Seed existiert für jedes Asset der Lieferliste genau eine Metadatendatei; `manifest build` liefert für noch fehlende Bilder den Zustand `planned` und keine toten Verweise; `verify` meldet sie als offen. |
| Pflichtfelder | Ein Eintrag ohne Kategorie, ohne Zustände oder ohne gültige Zonen bricht den Build mit Exit-Code `2` ab. |
| Duplikate und Reservierung | Eine doppelt vergebene oder als reserviert markierte ID blockiert den Build. |
| Fehlende Dateien | Ein Manifestverweis auf eine nicht vorhandene Datei bricht den Build ab; es entsteht niemals ein Manifest mit toten Verweisen. |
| Audiopools | Varianten entstehen ausschließlich aus vorhandenen Dateien; ein leerer Pool behält seine Fallback-Zuordnung und blockiert nicht. |
| Zonenabdeckung | Der Editor verweigert das Speichern bei Lücken; `cut` muss zusammenhängend und nicht leer sein. |
| Grenzvorrang | An der gemeinsamen Grenze liefert die Vorhersage `shaft`; eine Testposition in der Schneide liefert `cut`. |
| Skalierung | Gespeicherte Zonen liegen ausschließlich in `0..1`; dieselben Werte liefern in `small`, `medium` und `large` dasselbe Ergebnis. |
| Nichtüberschreiben | Der Build ändert weder Bilder noch authored Dateien; ein Test vergleicht Prüfsummen vor und nach dem Lauf. |
| Umfang | Der Build über alle Assets aus 00002 läuft in vertretbarer Zeit (Zielwert unter zwei Sekunden auf dem Entwicklungsgerät). |
| Trennung Messestand | Das Paket enthält die Werkzeugseite nicht als Messefunktion; der Stand liest nur `manifest.json`. |

## 6.2 Nachweise

| Nachweis | Inhalt | Zeitpunkt |
|-|-|-|
| Werkzeugabnahme | Vollständiger Durchlauf über alle Assets aus 00002 mit gültigem Manifest und bestandenem Verify-Lauf. | Vor B-02 des Hauptkonzepts |

# 7. Risiken

| Risiko | Kurzbeschreibung | Risikohöhe |
|-|-|-|
| R-01 | Die generierte `manifest.json` wird trotzdem von Hand geändert und beim nächsten Build still überschrieben. | hoch |
| R-02 | Werkzeug und Laufzeit driften über die Zeit zu unterschiedlichen Feldnamen auseinander. | mittel |
| R-03 | Der Zonen-Editor erzeugt formal gültige, fachlich aber falsche Zonen. | mittel |
| R-04 | Die Werkzeugkette wird größer und aufwendiger als der eigentliche Zweck. | mittel |

## 7.1 R-01: Handänderungen am generierten Manifest
### tl;dr
- **Favorit:** Generierte Datei deutlich kennzeichnen, Verify-Lauf als Pflicht und Änderungen nur über die Eingabedateien.

### Detail
Eine generierte Datei sieht aus wie eine normale Konfigurationsdatei. Ohne Gegenmaßnahme entstehen Änderungen, die der nächste Build verliert.

### Empfehlung
- **Favorit:** Kopfzeile `"$comment": "GENERATED - do not edit; see media-source and Assets/config/*.json"` im Manifest, `vgtools manifest verify` als fester Prüfschritt, und die lesbare `manifest.md` zum Gegenlesen statt Handarbeit.
- **Begründung:** Das ist eine Zeile Aufwand je Build und verhindert den teuersten Fehlerfall: eine Änderung, die im Betrieb fehlt, weil sie beim nächsten Build verschwindet.

## 7.2 R-02: Schema-Drift
### tl;dr
- **Favorit:** Ein einziges Schema in `VGame.Contracts`, das Build und Laufzeit gemeinsam verwenden.

### Detail
Ein separater Leser im Client und ein separater Schreiber im Werkzeug können unbemerkt auseinanderlaufen.

### Empfehlung
- **Favorit:** Manifest- und Konfigurationsschema in `VGame.Contracts` zentral halten, den Client über generierte Typen oder ein gemeinsames Schema daran binden und den Vertrag in Kap. 4 des Hauptkonzepts als einzige Quelle führen.
- **Begründung:** Ein Feld an einer Stelle definieren ist bereits Projektvorgabe; ein zweiter Leser mit eigener Feldliste würde sie verletzen.

## 7.3 R-03: Fachlich falsche Zonen
### tl;dr
- **Favorit:** Abdeckungsprüfung, Vorhersage und Sichtprüfung an der 96-px-Größe kombinieren.

### Detail
Ein Editor kann eine lückenlose, aber spielerisch unbrauchbare Zone erzeugen, etwa eine `cut`-Zone über dem gesamten Werkzeugkopf.

### Empfehlung
- **Favorit:** Live-Abdeckungsprüfung, Tap-/Swipe-Vorhersage im Editor und die Abnahmekriterien aus Kap. 6.2 dieses Konzepts und Kap. 3.7.1 aus 00002 anwenden.
- **Begründung:** Die Technik erkennt Lücken automatisch; die fachliche Beurteilung „richtige Kante“ bleibt eine kurze, gezielte Sichtprüfung statt einer vollständigen Handeingabe.

## 7.4 R-04: Übertechnisierung
### tl;dr
- **Favorit:** Reihenfolge aus Kap. 5 einhalten und nach AP-03 stoppen, wenn die Zeit knapp wird.

### Detail
Werkzeuge sind verlockend ausbaubar. Der Nutzen für die Messe entsteht jedoch schon mit Build plus Validierung.

### Empfehlung
- **Favorit:** Nach AP-03 ist ein nutzbares Manifest erreicht; AP-04 und AP-05 sind Komfortgewinne und werden erst danach umgesetzt.
- **Begründung:** Das entkoppelt den Nutzen der Werkzeugkette von der Fertigstellung der Oberflächen und hält den Zeitrahmen planbar.

# 8. Offene Punkte

| Offener Punkt | Kurzbeschreibung | Status |
|-|-|-|
| OP-01 | Aufteilung generiert versus authored | ✅ geklärt |
| OP-02 | Bedienform des Zonen-Editors | ✅ geklärt |
| OP-03 | Zeitpunkt der Manifestgenerierung | ✅ geklärt |
| OP-04 | Automatische Ableitung der Audiopools | ✅ geklärt |
| OP-05 | Gegenlesbarkeit der generierten Daten | ✅ geklärt |
| OP-06 | Vorab-Erzeugung der Manifesteinträge aus der Lieferliste | ✅ geklärt |

## 8.1 ✅ OP-01: Aufteilung generiert versus authored
### tl;dr
- Festgelegt: ein generiertes Manifest steht vier kleinen authored Dateien plus Zonen-Sidecars gegenüber.

### Detail
Die Aufteilung steht in Kap. 3.3. Ableitbare Felder werden nie von Hand gepflegt; authored Dateien werden nie vom Build überschrieben.

### Empfehlung
- **Favorit:** Aufteilung genau so übernehmen und in AP-01 festschreiben.
- **Begründung:** Sie reduziert die Handarbeit auf die Werte, die tatsächlich eine Entscheidung sind.

## 8.2 ✅ OP-02: Bedienform des Zonen-Editors
### tl;dr
- Festgelegt: Werkzeugseite im lokalen Host statt eigener Desktop-Anwendung.

### Detail
Die Begründung steht in Kap. 5 (Option C): dieselbe Zonenlogik wie die spätere Wertung, kein zweiter Anwendungslebenszyklus.

### Empfehlung
- **Favorit:** Werkzeugseite `/tools/zones` im Host implementieren und im Paket deaktivieren.
- **Begründung:** Die Vorhersage nutzt die echte Auswertung und verhindert eine zweite, abweichende Implementierung.

## 8.3 ✅ OP-03: Zeitpunkt der Manifestgenerierung
### tl;dr
- Festgelegt: Build zur Entwicklungs- und Paketierungszeit, nie am Messestand.

### Detail
Das Hauptkonzept verbietet frei editierbare Laufzeitskripte am Stand (Neu-Z-07), und der Kaltstart darf keine Werkzeuge benötigen.

### Empfehlung
- **Favorit:** Manifest beim Paketieren erzeugen, Version mitschreiben und am Stand nur lesen.
- **Begründung:** Das hält Offlinebetrieb und Betriebsruhe ein und verhindert Änderungen, die niemand geprüft hat.

## 8.4 ✅ OP-04: Automatische Ableitung der Audiopools
### tl;dr
- Festgelegt: Variantenlisten entstehen aus dem Audioordner, das Poolverhalten bleibt authored.

### Detail
Der Dateiname `<aktion>_<nn>.<format>` ist bereits Konvention im Hauptkonzept. Der Build nutzt sie zur Ableitung; die Laufzeit liest ausschließlich das Manifest.

### Empfehlung
- **Favorit:** Varianten automatisch ableiten, Pools mit Lautstärkeprofil, Fallback und Beutelregel in `audio.pools.json` authored führen.
- **Begründung:** Ein neuer Clip ist damit eine Datei plus Build und benötigt weder Code- noch Manifestarbeit von Hand.

## 8.5 ✅ OP-05: Gegenlesbarkeit der generierten Daten
### tl;dr
- Festgelegt: Der Build schreibt zusätzlich eine lesbare `manifest.md` und das ID-Register.

### Detail
Eine generierte JSON-Datei ist schlecht zu prüfen. Deshalb entsteht eine Revisionstabelle mit allen Einträgen zum Gegenlesen und für Reviews.

### Empfehlung
- **Favorit:** `manifest.md` als festen Bestandteil des Builds führen und im Review gegenlesen.
- **Begründung:** Die Prüfung bleibt ein Blick statt einer Dateianalyse; die JSON-Datei bleibt die einzige Laufzeitquelle.

## 8.6 ✅ OP-06: Vorab-Erzeugung der Manifesteinträge
### tl;dr
- Festgelegt: Die Metadatendateien aller geplanten Assets entstehen sofort nach der Lieferliste; später wird nur noch das Bild geliefert.

### Detail
Die Lieferliste aus [00002](00002-c-vollmer-assetlieferung.concept.md) Kap. 3.4 wird einmalig als Importliste übernommen. `vgtools manifest seed` legt daraus je Asset die Metadatendatei an und reserviert die ID. Bilddateien entstehen später und werden vom Build automatisch zugeordnet.

### Empfehlung
- **Favorit:** Importliste einmalig übernehmen, sofort seeden, danach Bilddateien nachliefern; der Build führt `status: planned` bis zur Lieferung.
- **Begründung:** Damit ist der mühsame Schritt vollständig vorweggenommen, und die spätere Lieferung besteht nur noch aus Dateien im richtigen Ordner.
- **Nächste Aktion:** Importliste in AP-05 anlegen und den Seed-Lauf in AP-07 gegen die echten Assets prüfen.
