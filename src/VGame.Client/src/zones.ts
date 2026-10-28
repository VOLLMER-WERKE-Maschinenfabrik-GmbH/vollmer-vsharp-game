import "./zones.css";
import { createElement, Save, Undo2, Redo2, MousePointer2, Crosshair, Hand, Trash2, RotateCcw, Eye, EyeOff, Maximize2, type IconNode } from "lucide";

type Point = { x: number; y: number };
type Zone = { zoneId: string; role: "cut" | "shaft"; points: number[][] };
type Zones = { assetId: string; canvas: { width: number; height: number }; pivot: Point; hitZones: Zone[] };
type Asset = { assetId: string; category: string; width: number; height: number; file: string; zones: Zones; version: string };
type EditMode = "edit" | "pivot" | "probe";

const root = document.querySelector<HTMLElement>("#zone-editor")!;
root.innerHTML = `
    <header class="editor-header"><div><div class="editor-brand">VOLLMER / AUTHORING</div><h1>Zonen-Editor</h1></div><button id="save-zones" class="editor-save" disabled>Speichern</button></header>
    <div class="editor-layout"><aside class="editor-sidebar">
        <div><label for="asset-select">Werkzeug</label><select id="asset-select" disabled></select><div class="asset-dimensions" id="asset-dimensions">Lädt …</div>
        <div class="editor-section"><h2>Trefferzone</h2><div class="zone-switch"><button data-role="cut" aria-pressed="true">Schneide</button><button data-role="shaft" aria-pressed="false">Schaft</button></div><ul id="point-list" class="point-list"></ul>
        <div class="editor-fields"><div><label for="point-horizontal">X</label><input id="point-horizontal" type="number" min="0" max="1" step="0.001" disabled></div><div><label for="point-vertical">Y</label><input id="point-vertical" type="number" min="0" max="1" step="0.001" disabled></div></div></div></div>
        <div><div class="editor-section"><h2>Pivot</h2><div class="editor-fields"><div><label for="pivot-horizontal">X</label><input id="pivot-horizontal" type="number" min="0" max="1" step="0.001" disabled></div><div><label for="pivot-vertical">Y</label><input id="pivot-vertical" type="number" min="0" max="1" step="0.001" disabled></div></div></div>
        <div class="editor-section"><h2>Testgeste</h2><div class="preview-mode"><button data-preview="tap" aria-pressed="true">Tap</button><button data-preview="swipe" aria-pressed="false">Swipe</button></div></div></div>
        <p id="editor-notification" class="editor-notification" role="alert"></p>
    </aside><section class="editor-workspace"><nav class="editor-toolbar" aria-label="Zonenwerkzeuge"></nav>
        <div class="canvas-viewport"><canvas id="zone-canvas" width="1024" height="256" aria-label="Werkzeugbild mit editierbaren Trefferzonen" tabindex="0"></canvas></div>
        <footer class="editor-footer"><span id="coverage-status" class="coverage-status" role="status">Keine Auswahl</span><span id="preview-result" role="status">—</span><span id="save-state">Entwurf</span></footer>
    </section></div>`;

const canvas = document.querySelector<HTMLCanvasElement>("#zone-canvas")!;
const context = canvas.getContext("2d")!;
const assetSelect = document.querySelector<HTMLSelectElement>("#asset-select")!;
const saveButton = document.querySelector<HTMLButtonElement>("#save-zones")!;
const notification = document.querySelector<HTMLElement>("#editor-notification")!;
const coverageStatus = document.querySelector<HTMLElement>("#coverage-status")!;
const saveState = document.querySelector<HTMLElement>("#save-state")!;
const pointList = document.querySelector<HTMLElement>("#point-list")!;
const pointHorizontal = document.querySelector<HTMLInputElement>("#point-horizontal")!;
const pointVertical = document.querySelector<HTMLInputElement>("#point-vertical")!;
const pivotHorizontal = document.querySelector<HTMLInputElement>("#pivot-horizontal")!;
const pivotVertical = document.querySelector<HTMLInputElement>("#pivot-vertical")!;
let asset: Asset | null = null;
let image: HTMLImageElement | null = null;
let role: Zone["role"] = "cut";
let mode: EditMode = "edit";
let previewMode = "tap";
let selectedPoint = -1;
let dragging = false;
let dirty = false;
let valid = false;
let busy = false;
let overlayVisible = true;
let zoom = 1;
let validationTimer: ReturnType<typeof setTimeout> | undefined;
let validationVersion = 0;
let loadVersion = 0;
let gesture: Point[] = [];
let gestureBefore = "";
let history: string[] = [];
let historyPosition = -1;
let sourcePixels = new Uint8ClampedArray();
let coverageMask = document.createElement("canvas");
const toolbar = document.querySelector<HTMLElement>(".editor-toolbar")!;
const buttons = new Map<string, HTMLButtonElement>();

function icon(node: IconNode): SVGElement {
    const element = createElement(node);
    element.setAttribute("aria-hidden", "true");
    return element;
}

function button(name: string, label: string, node: IconNode, action: () => void): void {
    const element = document.createElement("button");
    element.type = "button";
    element.className = "icon-button";
    element.title = label;
    element.setAttribute("aria-label", label);
    element.dataset.tool = name;
    element.append(icon(node));
    element.addEventListener("click", action);
    toolbar.append(element);
    buttons.set(name, element);
}

button("undo", "Rückgängig", Undo2, () => restore(-1));
button("redo", "Wiederholen", Redo2, () => restore(1));
button("edit", "Polygon bearbeiten", MousePointer2, () => selectMode("edit"));
button("pivot", "Pivot setzen", Crosshair, () => selectMode("pivot"));
button("probe", "Geste prüfen", Hand, () => selectMode("probe"));
button("delete", "Ausgewählten Punkt löschen", Trash2, () => {
    if (!asset || selectedPoint < 0) return;
    currentZone().points.splice(selectedPoint, 1);
    selectedPoint = -1;
    changed();
});
button("clear", "Aktuelle Zone leeren", RotateCcw, () => {
    if (!asset) return;
    currentZone().points = [];
    selectedPoint = -1;
    changed();
});
button("overlay", "Zonen anzeigen oder ausblenden", Eye, () => {
    overlayVisible = !overlayVisible;
    buttons.get("overlay")!.replaceChildren(icon(overlayVisible ? Eye : EyeOff));
    buttons.get("overlay")!.setAttribute("aria-pressed", String(overlayVisible));
    draw();
});
button("reload", "Zonendatei neu laden", RotateCcw, () => void loadAsset(assetSelect.value, true));
button("fit", "Bild einpassen", Maximize2, () => { if (asset) { fitImage(); draw(); scheduleValidation(); } });
const zoomControl = document.createElement("label");
zoomControl.className = "zoom-control";
zoomControl.innerHTML = `<span>Zoom</span><input id="zoom" type="range" min="0.25" max="3" step="0.05" value="1" aria-label="Zoom"><output id="zoom-value">100 %</output>`;
toolbar.append(zoomControl);
saveButton.prepend(icon(Save));

function currentZone(): Zone { return asset!.zones.hitZones.find(zone => zone.role === role)!; }
function pointAt(event: PointerEvent): Point {
    const bounds = canvas.getBoundingClientRect();
    return { x: Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width)), y: Math.max(0, Math.min(1, (event.clientY - bounds.top) / bounds.height)) };
}
function selectMode(next: EditMode): void {
    mode = next;
    for (const name of ["edit", "pivot", "probe"]) buttons.get(name)!.setAttribute("aria-pressed", String(name === mode));
}
function changed(record = true): void {
    if (!asset) return;
    dirty = true;
    valid = false;
    validationVersion++;
    document.querySelector("#preview-result")!.textContent = "—";
    if (record) {
        history = history.slice(0, historyPosition + 1);
        history.push(JSON.stringify(asset.zones));
        if (history.length > 80) history.shift();
        historyPosition = history.length - 1;
    }
    renderFields();
    draw();
    saveState.textContent = "Ungespeichert";
    scheduleValidation();
}
function restore(direction: number): void {
    if (!asset || busy || historyPosition + direction < 0 || historyPosition + direction >= history.length) return;
    historyPosition += direction;
    asset.zones = JSON.parse(history[historyPosition]) as Zones;
    selectedPoint = -1;
    changed(false);
}
function renderFields(): void {
    pointList.replaceChildren();
    if (!asset) return;
    for (const [index, point] of currentZone().points.entries()) {
        const item = document.createElement("li");
        const element = document.createElement("button");
        element.type = "button";
        element.setAttribute("aria-pressed", String(index === selectedPoint));
        element.textContent = `${index + 1}    ${point[0].toFixed(3)} / ${point[1].toFixed(3)}`;
        element.addEventListener("click", () => { selectedPoint = index; renderFields(); draw(); });
        item.append(element);
        pointList.append(item);
    }
    const point = currentZone().points[selectedPoint];
    pointHorizontal.disabled = pointVertical.disabled = busy || !point;
    pointHorizontal.value = point ? point[0].toFixed(3) : "";
    pointVertical.value = point ? point[1].toFixed(3) : "";
    pivotHorizontal.disabled = pivotVertical.disabled = busy;
    assetSelect.disabled = busy;
    pivotHorizontal.value = asset.zones.pivot.x.toFixed(3);
    pivotVertical.value = asset.zones.pivot.y.toFixed(3);
    buttons.get("undo")!.disabled = busy || historyPosition <= 0;
    buttons.get("redo")!.disabled = busy || historyPosition >= history.length - 1;
    buttons.get("delete")!.disabled = busy || !point;
    buttons.get("clear")!.disabled = busy;
    saveButton.disabled = !valid || busy || !dirty;
}
function polygonPath(zone: Zone): Path2D {
    const path = new Path2D();
    for (const [index, point] of zone.points.entries()) {
        if (index === 0) path.moveTo(point[0] * canvas.width, point[1] * canvas.height);
        else path.lineTo(point[0] * canvas.width, point[1] * canvas.height);
    }
    path.closePath();
    return path;
}
function draw(): void {
    context.clearRect(0, 0, canvas.width, canvas.height);
    if (!asset || !image) return;
    context.drawImage(image, 0, 0);
    const maskContext = coverageMask.getContext("2d", { willReadFrequently: true })!;
    maskContext.clearRect(0, 0, canvas.width, canvas.height);
    maskContext.fillStyle = "#fff";
    const zonePaths = asset.zones.hitZones.filter(zone => zone.points.length >= 3).map(polygonPath);
    for (const path of zonePaths) maskContext.fill(path);
    const covered = maskContext.getImageData(0, 0, canvas.width, canvas.height).data;
    const missing = maskContext.createImageData(canvas.width, canvas.height);
    let uncovered = 0;
    for (let offset = 3; offset < sourcePixels.length; offset += 4) {
        const pixel = (offset - 3) / 4;
        const horizontal = pixel % canvas.width + 0.5;
        const vertical = Math.floor(pixel / canvas.width) + 0.5;
        if (sourcePixels[offset] >= 128 && covered[offset] < 255 && !zonePaths.some(path => maskContext.isPointInPath(path, horizontal, vertical))) {
            missing.data[offset - 3] = 255;
            missing.data[offset] = 230;
            uncovered++;
        }
    }
    maskContext.putImageData(missing, 0, 0);
    context.drawImage(coverageMask, 0, 0);
    coverageStatus.textContent = uncovered ? `${uncovered.toLocaleString("de-DE")} Pixel ohne Zone` : "Silhouette abgedeckt · Prüfung …";
    coverageStatus.dataset.valid = "false";
    if (overlayVisible) for (const zone of asset.zones.hitZones) {
        const path = polygonPath(zone);
        context.strokeStyle = zone.role === "cut" ? "#008b9e" : "#b58200";
        context.fillStyle = zone.role === "cut" ? "#00c5d533" : "#ffc63233";
        context.lineWidth = Math.max(1, 2 / zoom);
        if (zone.points.length >= 3) context.fill(path);
        context.stroke(path);
        if (zone.role === role) for (const [index, point] of zone.points.entries()) {
            context.beginPath();
            context.arc(point[0] * canvas.width, point[1] * canvas.height, (index === selectedPoint ? 5 : 3) / zoom, 0, Math.PI * 2);
            context.fillStyle = index === selectedPoint ? "#ff7e00" : "#fff";
            context.fill();
            context.stroke();
        }
    }
    const pivot = asset.zones.pivot;
    context.strokeStyle = "#de184a";
    context.beginPath();
    context.moveTo(pivot.x * canvas.width - 10 / zoom, pivot.y * canvas.height);
    context.lineTo(pivot.x * canvas.width + 10 / zoom, pivot.y * canvas.height);
    context.moveTo(pivot.x * canvas.width, pivot.y * canvas.height - 10 / zoom);
    context.lineTo(pivot.x * canvas.width, pivot.y * canvas.height + 10 / zoom);
    context.stroke();
}
function scheduleValidation(): void {
    clearTimeout(validationTimer);
    saveButton.disabled = true;
    const version = validationVersion;
    validationTimer = setTimeout(() => void validate(version), 200);
}
async function validate(version: number): Promise<void> {
    if (!asset) return;
    const identifier = asset.assetId;
    try {
        const response = await fetch(`/tools/zones/api/validate/${identifier}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(asset.zones) });
        if (version !== validationVersion || asset?.assetId !== identifier) return;
        valid = response.ok;
        coverageStatus.dataset.valid = String(valid);
        if (valid) coverageStatus.textContent = "Abdeckung und Geometrie gültig";
        else if (!coverageStatus.textContent?.includes("Pixel ohne Zone")) coverageStatus.textContent = "Zonengeometrie ungültig";
    } catch {
        if (version !== validationVersion) return;
        valid = false;
        notification.textContent = "Die Zonenprüfung ist nicht erreichbar.";
    }
    renderFields();
}
async function loadAsset(identifier: string, confirmDiscard = false): Promise<void> {
    if (!identifier || busy) return;
    if (dirty && confirmDiscard && !confirm("Ungespeicherte Zonen verwerfen?")) { assetSelect.value = asset!.assetId; return; }
    const version = ++loadVersion;
    busy = true;
    assetSelect.disabled = true;
    validationVersion++;
    valid = false;
    saveButton.disabled = true;
    notification.textContent = "";
    document.querySelector("#preview-result")!.textContent = "—";
    try {
        const response = await fetch(`/tools/zones/api/asset/${identifier}`, { cache: "no-store" });
        if (!response.ok) throw new Error("Werkzeugdaten konnten nicht geladen werden.");
        const loaded: Asset = await response.json();
        const loadedImage = new Image();
        loadedImage.src = loaded.file;
        await loadedImage.decode();
        if (version !== loadVersion) return;
        asset = loaded;
        image = loadedImage;
        canvas.width = loaded.width;
        canvas.height = loaded.height;
        coverageMask = document.createElement("canvas");
        coverageMask.width = canvas.width;
        coverageMask.height = canvas.height;
        const source = document.createElement("canvas");
        source.width = canvas.width;
        source.height = canvas.height;
        source.getContext("2d")!.drawImage(image, 0, 0);
        sourcePixels = source.getContext("2d")!.getImageData(0, 0, canvas.width, canvas.height).data;
        dirty = false;
        selectedPoint = -1;
        history = [JSON.stringify(asset.zones)];
        historyPosition = 0;
        document.querySelector("#asset-dimensions")!.textContent = `${loaded.width} × ${loaded.height} px · ${loaded.version === "new" ? "Entwurf" : "Zonendatei"}`;
        saveState.textContent = loaded.version === "new" ? "Entwurf" : "Gespeichert";
        fitImage();
        renderFields();
        draw();
        scheduleValidation();
    } catch (error) { notification.textContent = error instanceof Error ? error.message : "Laden fehlgeschlagen."; }
    finally { busy = false; assetSelect.disabled = assetSelect.options.length === 0; renderFields(); }
}
function setZoom(value: number): void {
    zoom = value;
    canvas.style.width = `${canvas.width * zoom}px`;
    canvas.style.height = `${canvas.height * zoom}px`;
    document.querySelector<HTMLOutputElement>("#zoom-value")!.value = `${Math.round(zoom * 100)} %`;
    document.querySelector<HTMLInputElement>("#zoom")!.value = String(zoom);
}
function fitImage(): void {
    const viewport = document.querySelector<HTMLElement>(".canvas-viewport")!;
    setZoom(Math.min(1, Math.max(0.25, Math.min((viewport.clientWidth - 60) / canvas.width, (viewport.clientHeight - 60) / canvas.height))));
}

canvas.addEventListener("pointerdown", event => {
    if (!asset || busy) return;
    canvas.focus();
    canvas.setPointerCapture(event.pointerId);
    const point = pointAt(event);
    if (mode === "probe") { gesture = [point]; dragging = true; return; }
    gestureBefore = JSON.stringify(asset.zones);
    dragging = true;
    if (mode === "pivot") { asset.zones.pivot = point; changed(false); return; }
    const bounds = canvas.getBoundingClientRect();
    selectedPoint = currentZone().points.findIndex(vertex => Math.hypot((vertex[0] - point.x) * bounds.width, (vertex[1] - point.y) * bounds.height) < 10);
    if (selectedPoint < 0) { currentZone().points.push([point.x, point.y]); selectedPoint = currentZone().points.length - 1; changed(false); }
    else { renderFields(); draw(); }
});
canvas.addEventListener("pointermove", event => {
    if (!asset || !dragging) return;
    const point = pointAt(event);
    if (mode === "probe") { if (gesture.length < 31) gesture.push(point); return; }
    if (mode === "pivot") asset.zones.pivot = point;
    else if (selectedPoint >= 0) currentZone().points[selectedPoint] = [point.x, point.y];
    changed(false);
});
canvas.addEventListener("pointerup", event => {
    if (!dragging || !asset) return;
    dragging = false;
    if (mode !== "probe") {
        if (gestureBefore !== JSON.stringify(asset.zones)) changed();
        else scheduleValidation();
        return;
    }
    gesture.push(pointAt(event));
    if (!valid) { document.querySelector("#preview-result")!.textContent = "Zuerst gültige Zonen erforderlich"; return; }
    const points = previewMode === "tap" ? [gesture[0]] : gesture;
    const previewIdentifier = asset.assetId;
    const previewVersion = validationVersion;
    void fetch("/tools/zones/api/preview", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ zones: asset.zones, mode: previewMode, points }) })
        .then(async response => {
            const result = await response.json();
            if (asset?.assetId !== previewIdentifier || validationVersion !== previewVersion) return;
            const labels: Record<string, string> = { success: "Erfolg", shaft_hit: "Schafttreffer", wrong_sharpening: "Falsches Schärfen", miss: "Fehlschlag" };
            document.querySelector("#preview-result")!.textContent = response.ok ? `${labels[result.outcome] ?? result.outcome} · ${result.scoreDelta} Punkte` : result.error;
        }).catch(() => { notification.textContent = "Die Vorhersage ist nicht erreichbar."; });
});
canvas.addEventListener("pointercancel", () => {
    if (dragging && asset && mode !== "probe" && gestureBefore !== JSON.stringify(asset.zones)) changed();
    dragging = false;
});
canvas.addEventListener("keydown", event => {
    if (event.key === "Delete" || event.key === "Backspace") { event.preventDefault(); buttons.get("delete")!.click(); }
    if (event.ctrlKey && event.key.toLowerCase() === "z") { event.preventDefault(); restore(event.shiftKey ? 1 : -1); }
});
document.querySelectorAll<HTMLButtonElement>("[data-role]").forEach(element => element.addEventListener("click", () => {
    role = element.dataset.role as Zone["role"];
    selectedPoint = -1;
    document.querySelectorAll("[data-role]").forEach(button => button.setAttribute("aria-pressed", String((button as HTMLElement).dataset.role === role)));
    renderFields(); draw(); if (asset) scheduleValidation();
}));
document.querySelectorAll<HTMLButtonElement>("[data-preview]").forEach(element => element.addEventListener("click", () => {
    previewMode = element.dataset.preview!;
    document.querySelectorAll("[data-preview]").forEach(button => button.setAttribute("aria-pressed", String((button as HTMLElement).dataset.preview === previewMode)));
    selectMode("probe");
}));
for (const input of [pointHorizontal, pointVertical, pivotHorizontal, pivotVertical]) input.addEventListener("change", () => {
    if (!asset) return;
    const value = input.valueAsNumber;
    if (!Number.isFinite(value) || value < 0 || value > 1) { notification.textContent = "Koordinaten müssen zwischen 0 und 1 liegen."; renderFields(); return; }
    notification.textContent = "";
    if (input === pivotHorizontal) asset.zones.pivot.x = value;
    else if (input === pivotVertical) asset.zones.pivot.y = value;
    else if (selectedPoint >= 0) currentZone().points[selectedPoint][input === pointHorizontal ? 0 : 1] = value;
    changed();
});
document.querySelector<HTMLInputElement>("#zoom")!.addEventListener("input", event => { setZoom((event.target as HTMLInputElement).valueAsNumber); draw(); if (asset) scheduleValidation(); });
assetSelect.addEventListener("change", () => void loadAsset(assetSelect.value, true));
saveButton.addEventListener("click", () => {
    if (!asset || !valid || busy) return;
    busy = true;
    const identifier = asset.assetId;
    const savedDocument = JSON.stringify(asset.zones);
    const expectedVersion = asset.version;
    renderFields();
    void fetch(`/tools/zones/api/asset/${identifier}`, { method: "POST", headers: { "Content-Type": "application/json", "If-Match": expectedVersion }, body: savedDocument })
        .then(async response => {
            const result = await response.json();
            if (!response.ok) throw new Error(result.error);
            if (asset?.assetId !== identifier) return;
            asset.version = result.version;
            if (JSON.stringify(asset.zones) !== savedDocument) return;
            dirty = false;
            saveState.textContent = "Gespeichert";
            notification.textContent = "";
        }).catch(error => { notification.textContent = error instanceof Error ? error.message : "Speichern fehlgeschlagen."; })
        .finally(() => { busy = false; renderFields(); });
});
window.addEventListener("beforeunload", event => { if (dirty) event.preventDefault(); });
selectMode("edit");
buttons.get("overlay")!.setAttribute("aria-pressed", "true");
void fetch("/tools/zones/api/assets", { cache: "no-store" }).then(async response => {
    if (!response.ok) throw new Error("Der lokale Entwicklungseditor ist nicht verfügbar.");
    const assets: string[] = await response.json();
    for (const identifier of assets) assetSelect.add(new Option(identifier, identifier));
    assetSelect.disabled = assets.length === 0;
    if (assets.length === 0) notification.textContent = "Keine Werkzeugbilder vorhanden.";
    else await loadAsset(assets[0]);
}).catch(error => { notification.textContent = error instanceof Error ? error.message : "Editor nicht verfügbar."; });