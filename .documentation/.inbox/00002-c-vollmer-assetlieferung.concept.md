# Vollmer Messe-Game: Asset- und Bildlieferung

| | | |
|-|-|-|
| **Erstelldatum:** | 2026-09-28 |
| **Letzte Änderung:** | 2026-10-11 |
| **Issue:** | 00002 |

## Commit-Vorschlag für den finalen Gesamt-Commit

> **Status:** `vorläufig` — Der Vorschlag wird nach Umsetzung, Einarbeitung aller Code-Review-Findings und erfolgreicher finaler Review anhand der tatsächlich enthaltenen Änderungen aktualisiert. Es wird kein separater Konzept-Commit erstellt.

```text
[00002] Asset- und Bildlieferung des Vollmer Messe-Games festlegen

- Geplante Umsetzung: vollständige Bildlieferliste mit IDs, Mengen, Zustands-Overlays, Requisiten, Verdeckern und Ablagestruktur.
- Geplante Entscheidung: 12 Werkzeugbilder, 4 Zustände (`dull`, `in_progress`, `sharp`, `failed`), 12 Zustands-Overlays und eingebackene 2D-Tiefe statt Laufzeit-Blur auf der Kulisse.
- Geplante Absicherung: Silhouetten-, Alpha-, Zonenabdeckungs-, Tiefenprofil- und Manifest-Rundlauftests vor der Abnahme B-02.
- Risiken und offene Punkte: keine blockierenden; Chroma-Key-Reste auf dem orangefarbenen Boden sind das Hauptproduktionsrisiko.
```

# 0. Entscheidungsvorlage

| | |
|-|-|
| **Problem** | Für die Spielbilder fehlen konkrete Liefermengen, ein Zustandsmodell und die technische Erklärung der 2D-Tiefe. |
| **Lösung** | Dieses Konzept legt Anzahl, Varianten, Zustände, Störgegenstände, Ablage und Tiefenstaffelung der Bilder verbindlich fest. |
| **Entscheidungsbedarf** | Freigabe der Liefermengen (12 Werkzeugbilder, 12 Zustands-Overlays, 1 Bühne, 4 Requisiten, 6 Verdecker) und der eingebackenen Tiefenstaffelung. |
| **Top-Risiko** | Chroma-Key-Reste und Alpha-Fransen sind auf dem orangefarbenen Boden sofort sichtbar. |
| **Blockierende offene Punkte** | — entfällt — (OP-01 bis OP-05 sind geklärt). |

# 1. Kontext, Zielsetzung & Use Case

## 1.1 tl;dr
- Dieses Konzept beantwortet konkret: **wie viele Bilder**, **welche Zustände**, **wie viele Störgegenstände** und **wie die 2D-Tiefe technisch entsteht**.
- Verbindlicher Startumfang: **37 Assets / 35 Bildassets** mit klaren IDs; die Audio-Lieferlisten bleiben in [00001](00001-c-vollmer-messe-game.concept.md) Kap. 3.4.1 bis 3.4.4.
- Werkzeuge haben vier Zustände: `dull`, `in_progress`, `sharp`, `failed`; die Zustände sind **Overlays**, keine eigenen Werkzeugbilder.
- Die Tiefe wird **ins Bild eingebacken** (flachere, entsättigte Kulisse); zur Laufzeit kommen nur Skalierung, Tint, Kontaktschatten und optional minimaler Requisiten-Blur hinzu.

## 1.2 Ziele und Nicht-Ziele

| Ziel | Beschreibung |
|-|-|
| Z-01 | Eine abzählbare Bildlieferliste mit Dateinamen, IDs, Mengen und Erwartungswert je Asset liegt vor. |
| Z-02 | Das Zustandsmodell der Werkzeuge ist vollständig definiert und je Zustand mit einer Bilddatei hinterlegt. |
| Z-03 | Die Störgegenstände (Vordergrund-Verdecker und Requisiten) sind gezählt, in ihrer Wirkung begrenzt und abschaltbar. |
| Z-04 | Die 2D-Tiefe ist als reproduzierbares Verfahren mit konkreten Werten beschrieben, nicht als Geschmacksfrage. |
| Z-05 | Ablage, Benennung und ID-Vergabe sind mit der Solution-Struktur kompatibel und erzeugen kein Nachbearbeiten im Manifest. |
| Z-06 | Die Produktion ist mit Leonardo AI und Affinity reproduzierbar und ohne zusätzliche Designfreigabe abschließbar. |

| Nicht-Ziel (Scope) | Begründung |
|-|-|
| NZ-01 | Keine Audio-Lieferliste. | Aussagen zu Sprach-, UI- und Musikdateien bleiben allein in Kap. 3.4.1 bis 3.4.4 des Hauptkonzepts. |
| NZ-02 | Keine UI-, HUD- und Bedienhüllen-Grafiken. | Diese folgen dem Design-System und der Spielinszenierung aus Kap. 3.4 und 3.6 des Hauptkonzepts. |
| NZ-03 | Keine Werkzeuge und Skripte zur Manifestpflege. | Autorenwerkzeuge und Manifestgenerierung sind eigenes Konzept [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md). |
| NZ-04 | Keine Fotorealistik, keine 3D-Modelle, keine Echtzeit-Schatten. | Die Messeinteraktion braucht lesbare Silhouetten, keinen Realismus (Neu-Z-01 des Hauptkonzepts). |
| NZ-05 | Kein Laufzeit-Blur auf der Kulisse. | Ein Weichzeichnen der festen Kulisse kostet Bildrate ohne Gewinn; die Weichheit wird gemalt. |
| NZ-06 | Keine getrennte Overlay-Datei je Größenvariante. | Overlays hängen an der normalisierten `cut`-Zone und skalieren mit dem Werkzeug. |

## 1.3 Auslöser und Begriffe

Auslöser ist die Rückfrage zum Hauptkonzept: Die Spielregeln waren festgelegt, aber Menge, Varianten, Zustände und die technische Herkunft der Tiefenwirkung waren offen. Eine Produktion ohne diese Angaben hätte zu Vermeidungsaufwand und Nacharbeit in den Manifesten geführt.

**Begriffe:** *Asset* ist eine kleinste lieferbare Einheit mit eigener ID (Bild, Overlay, Effektatlas). *Master* ist die hochauflösende Quelldatei, die nicht ausgeliefert wird. *Lieferdatei* ist die normierte Laufzeitdatei. *Bühne* ist das Kulissenbild. *Requisite* ist ein dekoratives, bewegtes Element hinter der Zielebene. *Verdecker* (Störgegenstand) ist ein Vordergrundobjekt, das Ziele zeitlich begrenzt teilweise verdeckt.

# 2. Analyse / Ist-Zustand

## 2.1 tl;dr
- Das Hauptkonzept beschreibt Regeln, Zonen und Kategorien, aber **keine Mengen** und keinen Produktionsstand.
- Vorhanden sind eine Skizze ([game_view_sketch.png](game_view_sketch.png)), Leonardo AI als Generator und Affinity zum Freistellen und Normieren.
- Es existieren noch keine Applikation, keine Assets und kein Manifest; dieses Konzept ist daher voraussetzungsfrei umsetzbar.

## 2.2 Offene Fragen und beantwortete Lücke

Vor diesem Konzept waren folgende Punkte offen:

| Offene Frage | Antwort in diesem Konzept |
|-|-|
| Wie viele Werkzeugbilder und welche Varianten? | Kap. 3.4.1: 12 Bilder (4 Bohrer, 4 Fräser, 4 Sägeblätter), davon 2 PKD-Varianten |
| Wie viele Zustände und welcher Status gilt als zerstört? | Kap. 3.5: vier Zustände `dull`, `in_progress`, `sharp`, `failed` |
| Wie viele Zustandsbilder? | Kap. 3.4.2: 12 Overlays (3 Familien × 4 Zustände) |
| Wie viele Störgegenstände? | Kap. 3.4.3: 6 Vordergrund-Verdecker und 4 Requisiten |
| Wie entsteht der 2D-Tiefeneffekt (Blur, Opacity, Helligkeit)? | Kap. 3.6: eingebackene Luftperspektive plus Tint, Skalierung und Kontaktschatten |
| Wo liegen Master und Lieferdateien in der Solution? | Kap. 3.3 |

Die im Hauptkonzept beschriebenen Zonenregeln, das Zustandsprinzip (`stateOverlays`) und die Materialvorgaben bleiben unverändert gültig; dieses Konzept füllt sie mit Mengen und einem Ablageort.

# 3. Lösungskonzept / Soll-Zustand

## 3.1 tl;dr
- **37 Assets**: 12 Werkzeugbilder, 12 Zustands-Overlays, 1 Bühnenbild, 4 Requisiten, 6 Verdecker und 2 Effektatlanten.
- Werkzeugbilder sind zustandsneutral; die vier Zustände kommen als zonenverankerte Overlays darüber.
- Die Tiefe entsteht aus vier gebackenen Bildmitteln und vier sparsamen Laufzeitmitteln; die Kulisse wird nie zur Laufzeit unscharf.
- Repo-Ablage: unveränderliche Laufzeitassets unter `Assets/`, Arbeitsdateien unter `media-source/`, beides in einer Solution-Struktur nach Kap. 3.3.

## 3.2 Nicht-Ziele

| Nicht-Ziel (Implementierung) | Begründung |
|-|-|
| NZ-01 | Keine automatische Bildqualitätsfreigabe. | Der Generator liefert Ausgangsmaterial; Normierung und Sichtprüfung bleiben bewusst manuell. |
| NZ-02 | Keine Variantenexplosion durch zusätzliche Größenklassen. | `small`, `medium`, `large` (Kap. 3.4.5 des Hauptkonzepts) bleiben die einzigen Größenklassen. |
| NZ-03 | Keine eigene Bilddatei für `in_progress` über die Overlay-Regel hinaus. | Der Zustand ist kurz (≤ 600 ms) und wird als Overlay mit kurzer Skalierungs-/Tint-Animation gezeigt. |
| NZ-04 | Keine generierten Kollegen innerhalb des Kulissenbilds. | Personen müssen animierbar und abschaltbar bleiben und liegen deshalb als eigene Requisiten vor. |

## 3.3 Zielstruktur: Solution, Repository und Ablage

Diese Struktur ist die gemeinsame Ablage für Spiel und Werkzeuge; das Hilfstool-Konzept [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) erweitert ausschließlich den Ordner `tools/`.

```text
<Repo-Root>/
  VGame.sln
  src/
    VGame.Server/          ASP.NET-Core-Host (SignalR, Manifest- und Konfigurationsladen, Assets als Content)
    VGame.Shell/           Blazor-Betriebshülle (F-Tasten, Ranglisten, Datenschutz-Overlay)
    VGame.Client/          Phaser-/TypeScript-Spielclient
    VGame.Contracts/       DTOs, Manifest- und Konfigurationsschemas
    VGame.Domain/          Regeln: Punkte, Timer, Rollen, Zonenauswertung
  tests/
    VGame.Domain.Tests/    MSTest (Regeln, Zonen, Zustände)
    VGame.Server.Tests/    Hub-/Snapshot-/Persistenztests
    VGame.E2E.Tests/       Start, Gesten, Runde, Watcherbeitritt
  tools/                   (siehe 00003)
  Assets/                  Laufzeitassets, einzige Quelle für den Host
    config/
      manifest.json        generiert, nicht von Hand bearbeiten
      gameplay.json        Punkte, Dauer, Toleranzen
      audio.pools.json     Audiopools
      depth.profiles.json  Tiefenstaffelung (Kap. 3.6)
      privacy.de.json      Datenschutzhinweis (Kap. 3.8 des Hauptkonzepts)
      id-register.json     vergebene und reservierte IDs
    images/
      tools/               tool_0001.webp … tool_0012.webp
      overlays/            ovl_0001.webp … ovl_0012.webp
      background/          bg_0001.webp, cast_0001.png …, occ_0001.png …
      ui/                  Bedienhüllen- und HUD-Grafiken (Hauptkonzept)
    animations/
      laser/               fx_0001.json + fx_0001.png
      grind/               fx_0002.json + fx_0002.png
    audio/                 ui, laser, grind, ambient, music (Hauptkonzept)
    fonts/
    templates/             kategoriespezifische Vorlagen für die Validierung
  media-source/            Arbeitsdateien, nie ausgeliefert
    prompts/                 Prompt- und Seed-Protokoll je Asset
    leonardo/                Rohausgaben des Generators
    affinity/                freigestellte, normierte Master-PNG
    zone-authoring/          <assetId>.zones.json aus dem Zonen-Editor (00003)
  .documentation/
```

Regeln zur Ablage:

- `Assets/` ist die **einzige** Quelle für den Laufzeitpfad; die Dateinamen dort sind endgültig und werden nach der Lieferung nicht mehr umbenannt.
- `media-source/` enthält keine Laufzeitdatei und wird nicht paketiert.
- Der Host kopiert `Assets/` unverändert in das Paket; es gibt keine zweite Assetkopie im Build.
- `Assets/config/manifest.json` ist generiert. Authorene Werte stehen in `gameplay.json`, `audio.pools.json`, `depth.profiles.json` und in den Sidecars aus Kap. 4.

## 3.4 Bildlieferliste

### 3.4.1 Werkzeugbilder (12 Assets)

Ein Werkzeugbild enthält genau ein Werkzeug, zustandsneutral, auf transparentem Grund (Vorlage `tool-image`, Kap. 3.4.5 des Hauptkonzepts). Die relative Spielgröße steuert die Bildausnutzung, nicht der Prompt.

| ID | `toolType` | `sizeClass` | PKD | Ausnutzung | Zone `cut` | Zone `shaft` |
|-|-|-|-|-:|-|-|
| `tool_0001` | `drill` | `small` | nein | 45 % | Bohrerspitze und Hauptschneiden | Spiral- und Einspannschaft |
| `tool_0002` | `drill` | `medium` | nein | 62 % | Bohrerspitze und Hauptschneiden | Spiral- und Einspannschaft |
| `tool_0003` | `drill` | `large` | nein | 80 % | Bohrerspitze und Hauptschneiden | Spiral- und Einspannschaft |
| `tool_0004` | `drill` | `large` | ja | 80 % | PKD-bestückte Schneiden | Schaft |
| `tool_0005` | `milling_cutter` | `small` | nein | 45 % | Stirn- und Umfangsschneiden | Werkzeugkörper und Schaft |
| `tool_0006` | `milling_cutter` | `medium` | nein | 62 % | Stirn- und Umfangsschneiden | Werkzeugkörper und Schaft |
| `tool_0007` | `milling_cutter` | `large` | nein | 80 % | Stirn- und Umfangsschneiden | Werkzeugkörper und Schaft |
| `tool_0008` | `milling_cutter` | `large` | ja | 80 % | PKD-bestückte Schneiden | Werkzeugkörper und Schaft |
| `tool_0009` | `saw_blade` | `small` | nein | 45 % | markierte Sägezahnkante (grobe Teilung) | Blattkörper und Bohrung |
| `tool_0010` | `saw_blade` | `medium` | nein | 62 % | markierte Sägezahnkante (mittlere Teilung) | Blattkörper und Bohrung |
| `tool_0011` | `saw_blade` | `large` | nein | 80 % | markierte Sägezahnkante (mittlere Teilung) | Blattkörper und Bohrung |
| `tool_0012` | `saw_blade` | `medium` | nein | 62 % | markierte Sägezahnkante (feine Teilung) | Blattkörper und Bohrung |

**Begründung der Menge:** Vier Bilder je Familie ergeben genug Abwechslung für eine 60-Sekunden-Runde, ohne dass dieselbe Silhouette zweimal sichtbar hintereinander erscheint. Fünf Tap-Werkzeuge und sieben Swipe-Werkzeuge bilden die beiden Modi ab; zwei PKD-Varianten zeigen den Diamond-Bezug sichtbar, ohne eine eigene Silhouettenfamilie zu benötigen.

**Erweiterung ohne Folgekosten:** Weitere Werkzeuge sind zusätzliche Zeilen in dieser Tabelle plus ein Manifesteintrag; Zonen-Overlays wachsen nicht mit, weil Overlays an der Familien- und Zustandsachse hängen.

### 3.4.2 Zustands-Overlays (12 Assets)

Der Zustand wird **nicht** als zweites Werkzeugbild geliefert, sondern als Overlay über der `cut`-Zone (Kategorie `tool-overlay`). Overlays hängen an Familie und Zustand, nicht an der Größenklasse: Sie werden mit dem Werkzeug skaliert.

| ID | Familie | `state` | Datei | Darstellung |
|-|-|-|-|-|
| `ovl_0001` | Werkzeug allgemein | `dull` | `ovl_0001.webp` | stumpfe, grau-blaue Schneide mit kleinen Ausbrüchen |
| `ovl_0002` | Werkzeug allgemein | `in_progress` | `ovl_0002.webp` | Funken-/Glanzspur als kurzer Übergang (≤ 600 ms) |
| `ovl_0003` | Werkzeug allgemein | `sharp` | `ovl_0003.webp` | geschärfte, hell glänzende Schneide |
| `ovl_0004` | Werkzeug allgemein | `failed` | `ovl_0004.webp` | zerstörte Schneide: Risse, Brandspuren, abgeplatzte Ecken |
| `ovl_0005` | Bohrer | `dull` | `ovl_0005.webp` | wie `ovl_0001`, an der Bohrerspitze verankert |
| `ovl_0006` | Bohrer | `sharp` | `ovl_0006.webp` | geschärfte Bohrerspitze |
| `ovl_0007` | Bohrer | `failed` | `ovl_0007.webp` | ausgebrochene Bohrerspitze |
| `ovl_0008` | Fräser | `dull` | `ovl_0008.webp` | matte Umfangsschneiden |
| `ovl_0009` | Fräser | `sharp` | `ovl_0009.webp` | geschärfte Umfangsschneiden |
| `ovl_0010` | Fräser | `failed` | `ovl_0010.webp` | abgeplatzte Schneidenecken |
| `ovl_0011` | Sägeblatt | `dull` | `ovl_0011.webp` | stumpfe, verrundete Zähne |
| `ovl_0012` | Sägeblatt | `sharp` | `ovl_0012.webp` | geschärfte, helle Zahnkante |

**Aufbau der Liste:** drei Familien × vier Zustände = zwölf Zustandsdarstellungen. Die generische Reihe `ovl_0001` bis `ovl_0004` deckt `in_progress` und alle Fälle ab, in denen kein familienspezifisches Bild nötig ist; die familienspezifischen Reihen liefern die deutlich sichtbaren Zustände `dull`, `sharp` und `failed`. Das Sägeblatt nutzt für `failed` die generische Zahnbruch-Darstellung `ovl_0004`, weil ein gebrochenes Blatt bereits über den Blattkörper lesbar ist.

**Technische Vorgabe je Overlay:** 256 × 256 px transparent (Master 512 × 512 px), Anker `cut-zone`, Skalierung mit der Werkzeug-Bounding-Box, keine eigene Trefferzone, keine Auswirkung auf die Wertung.

### 3.4.3 Bühne, Requisiten und Störgegenstände (11 Assets)

| ID | Kategorie | Datei | Größe | Wirkung |
|-|-|-|-|-|
| `bg_0001` | `showroom-stage` | `bg_0001.webp` | Master 3840 × 2160, Lieferung 2560 × 1440 | feste Kulisse mit detaillfreier `stageArea`; enthält keine Personen |
| `cast_0001` | `showroom-cast` | `cast_0001.png` | ≥ 800 px längste Kante | Kollege in grauem Vollmer-Polo (gehend, Variante A) |
| `cast_0002` | `showroom-cast` | `cast_0002.png` | ≥ 800 px | Kollege in grauem Polo (Variante B, andere Silhouette) |
| `cast_0003` | `showroom-cast` | `cast_0003.png` | ≥ 800 px | Kollege mit Kaffeetasse (humorvolle Variante) |
| `cast_0004` | `showroom-cast` | `cast_0004.png` | ≥ 800 px | bewegtes Maschinendetail (Vibrations-/Bewegungsteil) |
| `occ_0001` | `foreground-occluder` | `occ_0001.webp` | ≥ 512 px | Schutzscheibe |
| `occ_0002` | `foreground-occluder` | `occ_0002.webp` | ≥ 512 px | Kabelträger und Kabelschlauch |
| `occ_0003` | `foreground-occluder` | `occ_0003.webp` | ≥ 512 px | Transportkorb |
| `occ_0004` | `foreground-occluder` | `occ_0004.webp` | ≥ 512 px | Fördergut-Kiste |
| `occ_0005` | `foreground-occluder` | `occ_0005.webp` | ≥ 512 px | Spänefilter-Streifen |
| `occ_0006` | `foreground-occluder` | `occ_0006.webp` | ≥ 512 px | Werkzeugwagen-Vorderteil |

**Requisiten (4):** Sie laufen immer **hinter** der Zielebene, sind nie Trefferzone und kosten keine Punkte. Für die Personen sind drei Silhouetten vorgesehen, damit ein wiederkehrender Kollege nicht als identische Kopie auffällt. Ein einziger Requisitentyp wäre billiger, wirkt aber nach kurzer Zeit wie ein Fehler; drei Varianten sind der kleinste glaubwürdige Satz.

**Störgegenstände (6 Verdecker):** Die Obergrenze liegt bewusst bei sechs, damit die Verdeckungsregeln aus Kap. 3.5 des Hauptkonzepts (maximal 40 % Trefferfläche, höchstens 450 ms, mindestens 650 ms vollsichtbar davor, keine Verdeckung in den ersten 15 s) auch bei maximaler Gleichzeitigkeit einhaltbar bleiben. Sechs Verdecker und drei Bewegungsprofile ergeben genug Abwechslung, ohne die Fairness der Runde zu gefährden.

| Bewegungsprofil | Beschreibung | Startzuordnung |
|-|-|-|
| `pass_horizontal` | durchläuft die Bühne von einer Seite zur anderen | `occ_0001`, `occ_0003` |
| `pass_diagonal` | zieht schräg durch den unteren Bildbereich | `occ_0002`, `occ_0005` |
| `rise_and_hold` | steigt kurz auf, hält und verschwindet | `occ_0004`, `occ_0006` |

### 3.4.4 Effektassets (2 Assets)

| ID | Kategorie | Dateien | Zweck |
|-|-|-|-|
| `fx_0001` | `laser-effect` | `fx_0001.json`, `fx_0001.png` | Laserblitz und Fokuspunkt; Erfolg/Fehler nur über Tint und Intensität |
| `fx_0002` | `grind-effect` | `fx_0002.json`, `fx_0002.png` | Schleiffunken und Funkenabriss entlang der Schneide |

Beide Atlanten bleiben geteilt; es entstehen keine getrennten Erfolgs- und Fehleratlanten.

### 3.4.5 Summen

| Gruppe | Assets | Master-Dateien | Laufzeitdateien |
|-|-:|-:|-:|
| Werkzeugbilder | 12 | 12 PNG 1024² | 12 WebP |
| Zustands-Overlays | 12 | 12 PNG 512² | 12 WebP 256² |
| Bühnenbild | 1 | 1 PNG 3840 × 2160 | 1 WebP 2560 × 1440 |
| Requisiten | 4 | 4 PNG | 4 PNG/WebP |
| Störgegenstände | 6 | 6 PNG | 6 WebP |
| Effektassets | 2 | 2 Quelldateien | 2 JSON + 2 PNG |
| **Gesamt** | **37** | **37** | **39** |

**Grobe Aufwandsschätzung (keine Zusage):** zwei bis drei Personentage für die Bildproduktion einschließlich Freistellen, Normieren, Zonen setzen und Sichtprüfung. Die Zustands-Overlays sind der kleinste Posten und werden vollständig aus der jeweils fertigen Werkzeugform abgeleitet.

## 3.5 Zustandsmodell der Werkzeuge

| Zustand | Sichtbar als | Auslöser | Wirkung auf die Wertung | Auswertung danach |
|-|-|-|-|-|
| `dull` | Ausgangszustand, `ovl_0001`-Reihe | Spawn | — | normal auswertbar |
| `in_progress` | kurzer Übergang, `ovl_0002` | Beginn einer gültigen Geste | — | Übergang, keine eigene Wertung |
| `sharp` | `ovl_0003`-Reihe | bestätigter `success` | einmalig die Erfolgspunkte | aus den Trefferzonen entfernt |
| `failed` | `ovl_0004`-Reihe | bestätigter `wrong_sharpening` oder `shaft_hit` | einmalig die Punkte der auslösenden Aktion | aus den Trefferzonen entfernt |

**Verbindliche Zustandsregeln (ergänzen Kap. 3.3.1 des Hauptkonzepts):**

- Ein Werkzeug durchläuft genau einen Ergebniswechsel: von `dull` zu `sharp` oder zu `failed`. `miss` verändert den Zustand nicht.
- `failed` bedeutet zerstört: Risse, Brandspuren und abgeplatzte Schneiden sind sichtbar; das Werkzeug bleibt bis zum Despawn sichtbar.
- Ein Kontakt mit einem `sharp`- oder `failed`-Werkzeug löst **kein** Ergebnis, keine Punkte und keinen Sound aus.
- `failed`-Werkzeuge verschwinden beschleunigt (`failedDespawnMs`, Startwert 600 ms nach dem auslösenden Ereignis), damit sie die Bühne und die Ziellast nicht unnötig belegen.
- Der Zustandswechsel ist Teil der Serverantwort `ActionResolved` über `ToolState`; die Anzeige ist an denselben `EventId` gebunden und wird nie lokal vorweggenommen.

## 3.6 2D-Tiefe: wie der Effekt entsteht

Die Tiefe ist **kein Laufzeit-Weichzeichner**, sondern eine Kombination aus gemalter Luftperspektive und vier sparsamen Laufzeitmitteln. Ziel ist der ruhige Moorhuhn-Eindruck: eine feste, flachere Kulisse und klar davorliegende Ziele.

| Nr. | Mittel | Wirkung | Wo | Kosten |
|-:|-|-|-|-|
| 1 | **Gemalte Luftperspektive** | Kulisse mit geringerem Kontrast, entsättigtem Boden und weichen Kanten | eingebacken in `bg_0001` | keine |
| 2 | **Kalte Absenkung der Kulisse** | Kulisse wirkt weiter hinten | Laufzeit-Tint, multiplikativ, 8–12 % Dunkelheit, leicht kühl | sehr gering |
| 3 | **Skalierung und Höhe** | Requisiten stehen höher im Bild und sind kleiner als Ziele | Anordnung im Layout | keine |
| 4 | **Helligkeits- und Sättigungsstaffelung** | Requisiten 88–95 % Helligkeit, leicht entsättigt; Ziele 100 % | Laufzeit-Tint je Requisite | gering |
| 5 | **Kontaktschatten** | weiche Ellipse unter Requisiten und Verdeckern (Alpha 20–35 %) | Laufzeit-Overlay | gering |
| 6 | **Konturschärfe** | Ziele mit dunkler Kontur und Drop-Shadow, Requisiten ohne harte Kontur | Assetgestaltung plus Laufzeit-Effekt | keine |
| 7 | **Bewegung** | Requisiten langsam, Ziele deutlich schneller, Verdecker am schnellsten | Profile | keine |
| 8 | **Optionaler Requisiten-Blur** | 0,5–1,0 px auf logischer Breite 1920 px, nur Requisiten | Laufzeitfilter, `enabled: false` im Startprofil | mittel |

**Antwort auf die Einzelfragen:** Unschärfe kommt nur optional und nur auf Requisiten zum Einsatz (Nr. 8), niemals auf der Kulisse. Transparenz (Opacity) wird nicht zur Tiefenstaffelung verwendet: Kulisse, Requisiten und Verdecker sind voll deckend; Alpha existiert nur in Freistellungen, Kontaktschatten und Effekten. Die Kulisse wird dunkler und kühler, Requisiten leicht dunkler, Ziele bleiben maximal hell und kontrastreich.

**Startwerte (`Assets/config/depth.profiles.json`, konfigurierbar):**

| Ebene | `scale` | `brightness` | `saturation` | `veil` (Dunkelheit) | `blurPx` |
|-|-:|-:|-:|-:|-:|
| Kulisse (`bg_0001`) | 1,00 | 1,00 | 0,90 | 0,10 | 0,0 |
| Requisiten (`cast_*`) | 0,95 | 0,92 | 0,92 | 0,04 | 0,0 (Startprofil) |
| Zielebene | 1,00 | 1,00 | 1,00 | 0,00 | 0,0 |
| Vordergrund (`occ_*`) | 1,15 | 0,98 | 1,00 | 0,00 | 0,0 |

Die Werte sind ein Startprofil und werden auf der Zielhardware nachjustiert; das Tiefenprofil ändert nur die Darstellung, niemals Trefferzonen oder Wertung. Eine Erhöhung von `blurPx` ist jederzeit über die Konfiguration möglich und benötigt keinen neuen Code.

## 3.7 Produktionsablauf

**Ablauf je Bildasset:**

1. **Prompt und Seed protokollieren** (`media-source/prompts/`) — je Asset ein Eintrag mit Prompt, Seed, Modell und Parametern.
2. **Rohbild erzeugen** mit Leonardo AI (Prompt-Vorlagen aus Kap. 3.4.6 des Hauptkonzepts).
3. **Freistellen und normieren** in Affinity: Chroma-Key oder Freistellfunktion, 1 px weiche Alpha-Kante, Zuschnitt auf die Bounding-Box, 8–10 % transparente Randreserve, Zielauflösung.
4. **Master ablegen** unter `media-source/affinity/<assetId>.png`.
5. **Lieferdatei erzeugen** (WebP, Zielauflösung) unter dem endgültigen Pfad in `Assets/`.
6. **Zonen setzen** mit dem Zonen-Editor aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md); Ergebnis ist `<assetId>.zones.json`.
7. **Manifest bauen** über die Werkzeugkette aus 00003; die Datei `Assets/config/manifest.json` wird nie von Hand bearbeitet. Die Einträge können bereits **vor der Bilderzeugung** aus der Lieferliste vorab erzeugt werden und stehen bis zur Lieferung als `planned`; nach der Lieferung bleibt nur noch der Build.
8. **Sichtprüfung** über die Checkliste in Kap. 3.7.1.

### 3.7.1 Sichtprüf-Checkliste je Bildasset

- genau ein Objekt pro Bild, keine Hände, keine Werkzeugstapel bei Werkzeugbildern;
- transparente Kante ohne hellen oder farbigen Saum (auf orangefarbenem Grund geprüft);
- Objekt vollständig, Mindestrand vorhanden, keine Beschnittkante;
- Silhouette bei 96 px längster Kante noch klar erkennbar;
- einheitliche Lichtrichtung von links oben, einheitliche Strichstärke und Farbfamilie;
- keine Schrift, keine Zahlen, kein Wasserzeichen, kein Logo;
- Zonen `cut` und `shaft` decken die Silhouette lückenlos ab; `cut` ist zusammenhängend und nicht leer;
- Overlay sitzt in allen drei Größenklassen deckungsgleich auf der `cut`-Zone;
- Kulisse: `stageArea` detaillfrei, keine Personen und keine Werkzeuge im Zielbereich.

## 3.8 IDs, Benennung und Reservierung

| Kategorie | ID-Muster | Dateimuster | Ablage |
|-|-|-|-|
| Werkzeugbild | `tool_####` | `tool_0001.webp` | `Assets/images/tools/` |
| Zustands-Overlay | `ovl_####` | `ovl_0001.webp` | `Assets/images/overlays/` |
| Bühnenbild | `bg_####` | `bg_0001.webp` | `Assets/images/background/` |
| Requisite | `cast_####` | `cast_0001.png` | `Assets/images/background/` |
| Verdecker | `occ_####` | `occ_0001.webp` | `Assets/images/background/` |
| Effektatlas | `fx_####` | `fx_0001.json` | `Assets/animations/<art>/` |
| Audio | `<aktion>_<nn>` | `cheer_high_01.wav` | `Assets/audio/<art>/` |

Regeln: Eine ID wird genau einmal vergeben und nach dem Entfernen dauerhaft reserviert. Die ID-Vergabe ist aufsteigend und lückenlos je Kategorie; die Datei im Laufzeitordner trägt immer die ID als Namen. Bedeutung und Fachdaten stehen ausschließlich im Manifest, nie im Dateinamen.

# 4. Contracts

## 4.1 tl;dr
- Je Asset existieren zwei kleine Autorendateien: `<assetId>.zones.json` (Geometrie) und `<assetId>.meta.json` (Herkunft, Lizenz, Prompt, Seed).
- Der generierte `manifest.json` ist Laufzeitvertrag und wird nicht von Hand geändert.
- Die Werkzeugkette aus 00003 liest die Autorendateien und schreibt das Manifest.

| Datei | Richtung | Inhalt |
|-|-|-|
| `media-source/zone-authoring/<assetId>.zones.json` | Editor -> Werkzeugkette | `{ assetId, canvas: { width, height }, pivot: {x,y}, hitZones: [{ zoneId, role, points[] }], cutDirectionDeg?, minStrokeLengthNormalized? }` in normalisierten Koordinaten `0..1` |
| `media-source/assets/<assetId>.meta.json` | Wizard/Hand -> Werkzeugkette | `{ assetId, category, toolType?, interaction?, sizeClass?, pkd?, states[], licenseStatus, source, generator, model, seed, prompt, createdUtc }` |
| `Assets/config/id-register.json` | Werkzeugkette -> alle | `{ category: { nextId, reservedIds[], assignedIds[] } }` |
| `Assets/config/manifest.json` | Werkzeugkette -> Host und Clients | generiertes Manifest (Schema unverändert nach Kap. 4 des Hauptkonzepts) |
| `Assets/config/depth.profiles.json` | Mensch -> Host und Clients | Tiefenstaffelung aus Kap. 3.6 |
| `Assets/templates/<kategorie>.template.json` | Mensch -> Validator | Pflichtfelder, Formate und Grenzwerte je Kategorie |

**Kompatibilitätsregel:** Dieses Konzept ändert das Manifest-Schema aus Kap. 4 des Hauptkonzepts nicht. Es ändert nur, **wer** die Datei schreibt: die Werkzeugkette aus [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) statt eines Menschen.

# 5. Implementierungsplan

## 5.1 tl;dr
- Die Umsetzung ist in sieben Arbeitspakete gegliedert; AP-01 legt Struktur und Vorlagen an, AP-02 bis AP-05 produzieren die Bilder, AP-06 überführt sie ins Manifest, AP-07 prüft ab.
- Die Produktion hängt nicht von der Anwendung ab und kann parallel zum Prototyp laufen.
- Als Tiefenverfahren ist die eingebackene Kulisse mit sparsamen Laufzeitmitteln der Favorit.

| AP | Ziel / Scope | Betroffene Komponenten | Abhängigkeiten | Validierung | Done-Kriterium |
|-|-|-|-|-|-|
| AP-01 | Ablage, Vorlagen und ID-Register anlegen. | Repo-Struktur Kap. 3.3, `media-source/`, `Assets/templates/`, `id-register.json` | — | Ordner- und Vorlagencheck gegen Kap. 3.3/3.8 | Alle Pfade und Vorlagen existieren, das ID-Register enthält die Start-IDs, kein Asset liegt am falschen Ort. |
| AP-02 | Bühne, Requisiten und Verdecker produzieren. | `bg_0001`, `cast_0001`–`cast_0004`, `occ_0001`–`occ_0006` | AP-01 | Checkliste Kap. 3.7.1 für Kulisse und Requisiten; `stageArea`- und Detaillarmutcheck | Elf Assets liegen normiert in `Assets/`, die Kulisse enthält keine Personen und eine detaillfreie `stageArea`. |
| AP-03 | Zwölf Werkzeugbilder produzieren. | `tool_0001`–`tool_0012` | AP-01 | Silhouette bei 96 px, Alpha-Kante auf orangefarbenem Grund, ein Werkzeug pro Bild | Zwölf zustandsneutrale Werkzeugbilder liegen normiert vor, PKD ist nur an der Schneide sichtbar. |
| AP-04 | Zwölf Zustands-Overlays produzieren. | `ovl_0001`–`ovl_0012` | AP-03 | Deckungsgleichheit auf der `cut`-Zone in allen drei Größenklassen | Alle vier Zustände sind je Familie sichtbar unterscheidbar und sitzen in allen Größenklassen deckungsgleich. |
| AP-05 | Effektatlanten bereitstellen und zuordnen. | `fx_0001`, `fx_0002`, Erfolgs-/Fehler-Tint | AP-01 | Sichtprüfung Erfolg gegen Fehler ausschließlich über Tint und Intensität | Zwei geteilte Atlanten sind vorhanden, Erfolg und Fehler sind ohne eigene Atlanten unterscheidbar. |
| AP-06 | Zonen und Zustände in Sidecars eintragen und Manifest bauen. | `<assetId>.zones.json`, `<assetId>.meta.json`, Werkzeugkette aus 00003 | AP-02, AP-03, AP-04, AP-05, AP-06 aus 00003 | Zonenabdeckung lückenlos, `shaft`-Vorrang, Manifest validiert | Das generierte Manifest enthält alle Assets dieses Konzepts, ist valide und ohne Handarbeit erzeugt. |
| AP-07 | Asset-Abnahme für B-02 des Hauptkonzepts durchführen. | Checkliste Kap. 3.7.1, Zonen-/Tiefenprofil-/Ziellastcheck | AP-06 | Vollständige Sicht-, Zonen- und Ziellastprüfung plus Stichprobe auf der Zielhardware | Die Abnahmeliste ist vollständig abgehakt, Tiefenprofil und Ziellastgrenzen sind auf der Zielhardware bestätigt. |

| Option | Vorteile | Nachteile |
|-|-|-|
| A: Tiefe vollständig gemalt | keine Laufzeitkosten, maximale Kontrolle über den Look | Kulisse wirkt schnell flach, Korrekturen erfordern neue Bilder |
| B: Tiefe vollständig zur Laufzeit (Blur/Tint/Opacity) | nachträglich justierbar, ein einziges Kulissenbild | Weichzeichnen kostet Bildrate auf der Zielhardware; Opacity-Staffelung wirkt schnell künstlich |
| C: Gemalte Luftperspektive plus sparsame Laufzeitmittel | Look ist stabil, Justierung per Konfiguration, keine Kosten auf der Kulisse | zwei Stellschrauben (Bild und Profil) müssen aufeinander abgestimmt werden |

**Favorit:** Option C.

**Begründung:** Die feste Kulisse bleibt bildratenneutral, während Helligkeit, Sättigung und optionale Requisitenunschärfe ohne neuen Build nachjustierbar bleiben. Gegenüber A bleibt die Abstimmung möglich, gegenüber B entfällt das teure Weichzeichnen der bildschirmfüllenden Fläche.

# 6. Tests

## 6.1 tl;dr
- Geprüft werden Silhouette, Alpha-Kante, Zonenabdeckung, Overlay-Deckung, Tiefenprofil und Ziellast.
- Die Abnahme stützt sich auf die Sichtprüf-Checkliste aus Kap. 3.7.1 und den Manifest-Rundlauf aus 00003.
- Kein Asset gilt als geliefert, bevor es auf orangefarbenem Grund und in allen Größenklassen geprüft wurde.

| Bereich | Prüffall / Akzeptanzkriterium |
|-|-|
| Alpha-Kante | Auf dem orangefarbenen Boden ist kein heller, grauer oder magentafarbener Saum sichtbar; die Kante ist an der kleinsten Spielgröße glatt. |
| Silhouette | Jedes Werkzeug ist bei 96 px längster Kante eindeutig erkennbar und von den anderen Familien unterscheidbar. |
| Objektanzahl | Jedes Werkzeugbild enthält genau ein Werkzeug; die Kulisse enthält keine Personen und keine Werkzeuge. |
| Zonenabdeckung | `cut` und `shaft` decken die opake Silhouette lückenlos ab, `cut` ist zusammenhängend und nicht leer, an der gemeinsamen Grenze wertet `shaft`. |
| Zustands-Overlays | Alle vier Zustände sind je Familie sichtbar unterscheidbar; das Overlay sitzt in `small`, `medium` und `large` deckungsgleich auf der `cut`-Zone. |
| Zustandsregeln | Ein Werkzeug wechselt genau einmal von `dull` nach `sharp` oder `failed`; `sharp` und `failed` sind nicht erneut auswertbar, lösen keine Punkte und keinen Sound aus; `failed` verschwindet beschleunigt. |
| Tiefenstaffelung | Kulisse ohne Blur; Requisiten mit den Profilwerten aus Kap. 3.6 sichtbar hinter der Zielebene; Ziele bleiben maximal kontrastreich; Tiefenprofilwerte sind ohne Build änderbar. |
| Bühne und Ziellast | `stageArea` ≥ 55 % der Canvasfläche und detaillfrei; bei maximaler Zielzahl bleibt die Werkzeugfläche unter 45 % der `stageArea` und der Mindestabstand gewahrt. |
| Requisiten und Verdecker | Requisiten verdecken nie ein Ziel und sind keine Trefferzone; kein Verdecker überschreitet 40 % Trefferfläche oder 450 ms; in den ersten 15 s erscheint keine Verdeckung. |
| Ablage und IDs | Jedes Asset liegt unter dem Pfad aus Kap. 3.8, trägt seine ID als Dateinamen, und keine ID ist doppelt oder wiederverwendet. |
| Manifest-Rundlauf | Ein zweiter Build erzeugt dasselbe Manifest; ein entferntes Asset führt zu einem Validierungsfehler statt zu einem stillen Austausch. |

## 6.2 Nachweis im Messebetrieb

| Nachweis | Inhalt | Zeitpunkt |
|-|-|-|
| B-02 (Hauptkonzept) | Vollständige Lieferung nach Kap. 3.4 mit 37 Assets, gültigen Zonen, validem Manifest und bestandener Sichtprüf-Checkliste. | Vor AP-06 des Hauptkonzepts |

# 7. Risiken

| Risiko | Kurzbeschreibung | Risikohöhe |
|-|-|-|
| R-01 | Chroma-Key-Reste und Alpha-Fransen werden auf dem orangefarbenen Boden sichtbar. | hoch |
| R-02 | Zustands-Overlays sitzen über die Größenklassen nicht deckungsgleich auf der `cut`-Zone. | mittel |
| R-03 | Zu viele oder zu große Störgegenstände machen die Runde unfair oder unruhig. | mittel |
| R-04 | Der Produktionsaufwand von 37 Assets verzögert die Messefreigabe. | mittel |

## 7.1 R-01: Freistellungsränder auf orangefarbenem Grund
### tl;dr
- **Favorit:** Freistellung immer auf orangefarbenem Probezgrund prüfen und bei Bedarf mit 1 px Alpha-Kante nacharbeiten.

### Detail
Leonardo AI liefert Rasterbilder; nach dem Chroma-Key bleiben häufig halbtransparente Randpixel der Hintergrundfarbe. Auf der gelben Vollmer-Fläche fiel das kaum auf, auf gesättigtem Orange sofort.

### Empfehlung
- **Favorit:** Nach dem Freistellen jeden Rand gegen einen orangefarbenen Probezgrund prüfen, die Kante mit 1 px weicher Alpha-Kante nachführen und magenta- oder grünfarbige Reste entfernen.
- **Begründung:** Das ist ein einzelner zusätzlicher Prüfschritt je Asset und verhindert den auffälligsten Qualitätsfehler der gesamten Bildlieferung.

## 7.2 R-02: Overlay-Deckung über Größenklassen
### tl;dr
- **Favorit:** Overlays an der normalisierten `cut`-Zone verankern und in allen drei Größenklassen prüfen.

### Detail
Ein Overlay, das für die mittlere Größe passt, kann bei `small` über die Schneide hinausragen oder bei `large` zu klein wirken.

### Empfehlung
- **Favorit:** Anker ausschließlich über die `cut`-Zone, Skalierung mit der Werkzeug-Bounding-Box und eine Prüfung bei `small`, `medium` und `large` als Abnahmekriterium.
- **Begründung:** Die Zone ist bereits die einzige Trefferautorität; dieselbe Geometrie als Anker vermeidet einen zweiten, abweichenden Bezugsrahmen.

## 7.3 R-03: Fairness der Verdeckungen
### tl;dr
- **Favorit:** Sechs Verdecker mit den bestehenden Verdeckungsgrenzen und deaktivierbarer Startkonfiguration.

### Detail
Störgegenstände erzeugen Humor, können aber Treffer verhindern. Das Hauptkonzept erlaubt bereits maximal 40 % Trefferfläche für höchstens 450 ms nach mindestens 650 ms Vollsichtbarkeit.

### Empfehlung
- **Favorit:** Obergrenze von sechs Verdeckern, drei Bewegungsprofile, Verdeckungen im Betreiberprofil abschaltbar; bei Beschwerden im Test zuerst `occ_0006` und `occ_0004` deaktivieren.
- **Begründung:** Der Humor bleibt erhalten, während die Fairnessregeln aus Kap. 3.5 des Hauptkonzepts nachweisbar einhaltbar bleiben.

## 7.4 R-04: Produktionsumfang
### tl;dr
- **Favorit:** Erst die zwölf Werkzeuge und die Kulisse, danach Overlays und Verdecker; die Messefreigabe hängt nicht an den Verdeckern.

### Detail
37 Assets sind in wenigen Tagen produzierbar, aber nicht an einem Nachmittag. Verzögert sich die Produktion, fehlen zuerst die dekorativen Elemente, nicht die spielrelevanten.

### Empfehlung
- **Favorit:** Reihenfolge aus Kap. 5 einhalten (AP-02 und AP-03 zuerst), Verdecker und zusätzliche Requisiten erst nach der Spielbarkeit produzieren und die Anzahl vor der Messe nicht erhöhen.
- **Begründung:** Die Spielbarkeit hängt an Werkzeugen, Zonen und Kulisse; Dekoration ist wertvoll, aber nicht eintrittskritisch.

# 8. Offene Punkte

| Offener Punkt | Kurzbeschreibung | Status |
|-|-|-|
| OP-01 | Anzahl und Varianten der Werkzeugbilder | ✅ geklärt |
| OP-02 | Zustandsmodell und Anzahl der Zustandsbilder | ✅ geklärt |
| OP-03 | Anzahl der Requisiten und Störgegenstände | ✅ geklärt |
| OP-04 | Technik der 2D-Tiefe | ✅ geklärt |
| OP-05 | Ablage und Struktur der Solution | ✅ geklärt |

## 8.1 ✅ OP-01: Anzahl und Varianten der Werkzeugbilder
### tl;dr
- Festgelegt: zwölf Werkzeugbilder, vier je Familie, zwei davon PKD.

### Detail
Die Aufteilung ist fünf Tap-Werkzeuge (Bohrer und Fräser) und sieben Swipe-Werkzeuge (Sägeblätter), siehe Kap. 3.4.1. Weitere Werkzeuge sind reine Erweiterungen ohne Codeumbau.

### Empfehlung
- **Favorit:** Genau die zwölf Bilder aus Kap. 3.4.1 produzieren.
- **Begründung:** Vier Varianten je Familie verhindern sichtbare Wiederholung innerhalb einer Runde und halten die Produktion bei einem Aufwand von zwei bis drei Personentagen.

## 8.2 ✅ OP-02: Zustandsmodell und Anzahl der Zustandsbilder
### tl;dr
- Festgelegt: vier Zustände `dull`, `in_progress`, `sharp`, `failed` mit zwölf Overlay-Dateien.

### Detail
`failed` ist der vom Auftraggeber gewünschte Zerstörungszustand. Er ist kein eigenes Werkzeugbild, sondern ein Overlay und eine Regel: falsch bearbeitete Werkzeuge sind bis zum Despawn nicht mehr auswertbar.

### Empfehlung
- **Favorit:** Zustandsmodell und Overlay-Liste aus Kap. 3.4.2 und Kap. 3.5 umsetzen; die Zustandsregeln in Kap. 3.3.1 und 3.3 des Hauptkonzepts ergänzen.
- **Begründung:** Zustandsbilder als Overlays wachsen nicht mit der Werkzeugmenge und halten die Trefferzonenlogik unverändert.

## 8.3 ✅ OP-03: Anzahl der Requisiten und Störgegenstände
### tl;dr
- Festgelegt: vier Requisiten (drei Personen, ein Maschinendetail) und sechs Vordergrund-Verdecker.

### Detail
Die Mengen sind in Kap. 3.4.3 begründet; die Bewegungsprofile sind auf drei Profile verteilt.

### Empfehlung
- **Favorit:** Vier Requisiten und sechs Verdecker produzieren, Verdeckungen zusätzlich konfigurierbar abschaltbar halten.
- **Begründung:** Das ist der kleinste Satz, der abwechslungsreich wirkt und die Fairnessgrenzen aus Kap. 3.5 des Hauptkonzepts einhält.

## 8.4 ✅ OP-04: Technik der 2D-Tiefe
### tl;dr
- Festgelegt: gemalte Luftperspektive plus Tint, Skalierung und Kontaktschatten; Blur nur optional auf Requisiten.

### Detail
Das Verfahren und seine Startwerte stehen in Kap. 3.6. Die Kulisse wird nie zur Laufzeit weichgezeichnet, und Transparenz wird nicht zur Tiefenstaffelung verwendet.

### Empfehlung
- **Favorit:** Option C aus Kap. 5 verwenden.
- **Begründung:** Der Look ist stabil, ohne Bildrate zu kosten, und bleibt über `depth.profiles.json` ohne Build nachjustierbar.

## 8.5 ✅ OP-05: Ablage und Struktur der Solution
### tl;dr
- Festgelegt: Laufzeitassets unter `Assets/`, Arbeitsdateien unter `media-source/`, Anwendung unter `src/` mit Namen nach Kap. 3.3.

### Detail
Die Struktur aus Kap. 3.3 ist die gemeinsame Basis für Spiel und Werkzeuge; [00003](00003-c-vollmer-manifest-und-hilfstools.concept.md) ergänzt nur `tools/`.

### Empfehlung
- **Favorit:** Struktur unverändert übernehmen und AP-01 des Hauptkonzepts damit schneiden.
- **Begründung:** Ein einziger Laufzeitassetpfad ohne Build-Kopie vermeidet doppelte Wahrheiten zwischen Entwicklung und Messebetrieb.
