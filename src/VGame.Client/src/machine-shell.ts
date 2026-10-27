import { createElement, Gauge, Waypoints, PanelTop, Stamp, Wrench, ShieldCheck, Settings, Egg, Zap, Cog, RotateCcw, Ruler, Volume2, Volume1, VolumeX, Hand, X, type IconNode } from "lucide";

type ShellCommand = "start" | "laser" | "saw";
type WorkshopEventSettings = { enabled: boolean; durationMilliseconds: number; cooldownMilliseconds: number };
type WorkshopEventOptions = { enabled: boolean; events: Record<string, WorkshopEventSettings> };
type ShellState = {
    score: number;
    combo: number;
    remainingMs: number;
    mode: "laser" | "saw";
    phase: "attract" | "playing" | "results";
    canStart: boolean;
    canSelectMode: boolean;
    canRequestRole: boolean;
    activePlayer: boolean;
    targetCount: number;
};

export class MachineShell {
    private readonly root: HTMLElement;
    private readonly commands = new Map<string, HTMLButtonElement[]>();
    private readonly controller = new AbortController();
    private readonly watcher: boolean;
    private volume = 0;
    private demonstration = false;
    private state?: ShellState;
    private eventTimer?: ReturnType<typeof setTimeout>;
    private readonly eventButtons = new Map<string, HTMLButtonElement>();
    private readonly eventCooldowns = new Map<string, number>();
    private eventCooldownTimer?: ReturnType<typeof setTimeout>;
    private eventOptions?: WorkshopEventOptions;
    private eventConfigurationUnavailable = false;
    private audioStatus = "";
    private commandHandler: (command: ShellCommand) => void = () => {};
    private soundHandler: (volume: number) => void = () => {};
    private actionTimer?: ReturnType<typeof setTimeout>;

    constructor(watcher: boolean) {
        this.watcher = watcher;
        this.root = document.getElementById("game")!;
        this.root.className = `machine-shell${watcher ? " is-watcher" : ""}`;
        this.root.innerHTML = `
            <section class="machine-screen">
                <aside class="machine-status" aria-label="Maschinenstatus">
                    <div class="machine-brand"><img src="/Assets/images/ui/vollmer-logo.png" alt="VOLLMER"></div>
                    <dl class="readouts">
                        <dt>PROGRAMM</dt><dd class="program-value">GAME01</dd>
                        <dt>PUNKTE</dt><dd data-readout="score">00000</dd>
                        <dt>ZEIT</dt><dd data-readout="time">01:00</dd>
                        <dt>COMBO</dt><dd class="combo-value" data-readout="combo">×0</dd>
                    </dl>
                    <div class="rotary-axes"><span>A <b data-readout="angle">0.0°</b></span><span>C <b>0.0°</b></span></div>
                    <div class="side-modes"></div>
                    <div class="machine-cell" aria-hidden="true">
                        <div class="machine-rail"></div><div class="machine-carriage"><div class="machine-spindle"></div><div class="machine-beam"></div><img class="machine-wheel" src="/Assets/images/tools/tool_0010.webp" alt=""></div>
                        <div class="machine-bed"></div><div class="machine-workpiece"></div>
                    </div>
                    <div class="diagnostic-grid" aria-label="Rundenstatus"></div>
                    <div class="position-title">Position</div>
                    <dl class="position-readouts">
                        <dt>A1</dt><dd data-readout="axis-angle">0.0°</dd>
                        <dt>B1</dt><dd>0.0°</dd><dt>E1</dt><dd>0.0°</dd>
                        <dt>X1</dt><dd data-readout="axis-x">0.0 mm</dd>
                        <dt>Y1</dt><dd data-readout="axis-y">0.0 mm</dd>
                        <dt>Z1</dt><dd data-readout="axis-z">0.0 mm</dd>
                    </dl>
                    <div class="connection-status" role="status">VERBINDE …</div>
                </aside>
                <div class="center-panel">
                    <div class="stage-frame"><div id="game-stage" aria-label="Spielbühne"></div>
                        <div class="gesture-demo" hidden aria-label="Gesten-Demonstration"></div>
                        <div class="shell-error" role="alert" hidden></div>
                    </div>
                    <nav class="command-strip" aria-label="Spielbefehle"></nav>
                </div>
            </section>
            <button class="event-drawer-toggle" type="button" aria-label="Ereignisleiste öffnen" aria-expanded="false"></button>
            <aside class="event-rail" aria-label="Werkstatt-Spielereien"></aside>
            <nav class="function-keys" aria-label="Funktionstasten"></nav>`;

        const entries = [
            ["start", "Start"], ["laser", "Laser"], ["saw", "Scheibe"],
            ["instructions", "Anleitung"], ["highscore", "Highscore"], ["sound", "Ton"]
        ];
        for (const [index, [command, label]] of entries.entries()) {
            const commandButton = this.makeButton(label, command, "command-button");
            commandButton.textContent = label;
            const keyButton = this.makeButton(label, command, "function-key");
            keyButton.textContent = `F${index + 1}`;
            keyButton.setAttribute("aria-label", `F${index + 1} ${label}`);
            this.root.querySelector(".command-strip")!.append(commandButton);
            this.root.querySelector(".function-keys")!.append(keyButton);
        }
        for (const [command, label, icon] of [["laser", "Laser", Zap], ["saw", "Scheibe", Cog]] as const) {
            const button = this.makeButton(label, command, "side-mode");
            button.append(this.icon(icon), document.createTextNode(label));
            this.root.querySelector(".side-modes")!.append(button);
        }
        for (const [icon, label, readout] of [
            [Cog, "Ziele", "targets"], [Settings, "Modus", "mode"], [RotateCcw, "Combo", "diagnostic-combo"],
            [Ruler, "Rundenzeit", "diagnostic-time"], [Gauge, "Zustand", "phase"], [Zap, "Punkte", "diagnostic-score"]
        ] as const) {
            const cell = document.createElement("div");
            cell.className = "diagnostic-cell";
            cell.title = label;
            cell.append(this.icon(icon));
            const value = document.createElement("span");
            value.dataset.readout = readout;
            value.textContent = "–";
            cell.append(value);
            this.root.querySelector(".diagnostic-grid")!.append(cell);
        }
        for (const [icon, label, effect] of [
            [Gauge, "Messuhr", "gauge"], [Waypoints, "Förderband", "conveyor"], [PanelTop, "Schutzscheiben-Wischer", "wiper"],
            [Stamp, "Prüfstempel", "stamp"], [Wrench, "Werkzeugwechsel", "tool"], [ShieldCheck, "Prüfsiegel", "shield"],
            [Settings, "Antrieb", "drive"], [Egg, "Überraschung", "egg"]
        ] as const) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "event-button";
            button.title = label;
            button.setAttribute("aria-label", label);
            button.disabled = true;
            button.dataset.event = effect;
            button.append(this.icon(icon));
            button.addEventListener("click", () => this.triggerEvent(button, effect), { signal: this.controller.signal });
            this.root.querySelector(".event-rail")!.append(button);
            this.eventButtons.set(effect, button);
        }
        const drawer = this.root.querySelector<HTMLButtonElement>(".event-drawer-toggle")!;
        drawer.append(this.icon(Settings));
        drawer.hidden = watcher;
        drawer.addEventListener("click", () => {
            const expanded = this.root.classList.toggle("events-open");
            drawer.setAttribute("aria-expanded", String(expanded));
            drawer.setAttribute("aria-label", expanded ? "Ereignisleiste schließen" : "Ereignisleiste öffnen");
            drawer.replaceChildren(this.icon(expanded ? X : Settings));
        }, { signal: this.controller.signal });
        this.root.querySelector(".gesture-demo")!.append(this.icon(Hand));
        document.addEventListener("keydown", event => {
            if (event.repeat || event.altKey || event.ctrlKey || event.metaKey) return;
            const index = ["F1", "F2", "F3", "F4", "F5", "F6"].indexOf(event.key);
            if (index < 0) return;
            event.preventDefault();
            this.commands.get(entries[index][0])?.[0].click();
        }, { signal: this.controller.signal });
        this.updateSoundButton();
        this.setEnabled("highscore", false);
        for (const button of this.commands.get("highscore")!) button.title = "Highscore – noch nicht verfügbar";
        for (const command of ["start", "laser", "saw"]) this.setEnabled(command, false);
        if (!watcher) void this.loadEventOptions();
    }

    bind(onCommand: (command: ShellCommand) => void, onSound: (volume: number) => void): void {
        this.commandHandler = onCommand;
        this.soundHandler = onSound;
        onSound(this.watcher ? 0 : [1, 0.3, 0][this.volume]);
    }

    update(state: ShellState): void {
        this.state = state;
        this.readout("score", state.score < 0 ? `-${Math.abs(state.score).toString().padStart(4, "0")}` : state.score.toString().padStart(5, "0"));
        this.readout("combo", `×${state.combo}`);
        this.readout("diagnostic-combo", String(state.combo));
        this.readout("targets", String(state.targetCount));
        this.readout("mode", state.mode === "laser" ? "L" : "S");
        this.readout("phase", state.phase === "playing" ? "RUN" : "STOP");
        this.readout("diagnostic-score", String(state.score));
        this.setTime(state.remainingMs);
        this.setPressed("laser", state.mode === "laser");
        this.setPressed("saw", state.mode === "saw");
        this.setEnabled("start", state.canStart || state.canRequestRole);
        this.setEnabled("laser", state.canSelectMode);
        this.setEnabled("saw", state.canSelectMode);
        this.commands.get("start")![0].textContent = state.canRequestRole ? "Übernehmen" : "Start";
        this.root.dataset.mode = state.mode;
        this.root.dataset.phase = state.phase;
        this.setConnection(state.activePlayer ? "SPIELER · AKTIV" : "WATCHER · PASSIV", false);
    }

    setTime(remainingMs: number): void {
        const seconds = Math.max(0, Math.ceil(remainingMs / 1000));
        this.readout("time", `${Math.floor(seconds / 60).toString().padStart(2, "0")}:${(seconds % 60).toString().padStart(2, "0")}`);
        this.readout("diagnostic-time", String(seconds));
    }

    setConnection(message: string, error: boolean): void {
        const status = this.root.querySelector<HTMLElement>(".connection-status")!;
        status.textContent = message;
        status.classList.toggle("is-error", error);
        if (error) for (const command of ["start", "laser", "saw"]) this.setEnabled(command, false);
    }

    setError(message: string): void {
        const error = this.root.querySelector<HTMLElement>(".shell-error")!;
        const visibleMessage = message || this.audioStatus || (this.eventConfigurationUnavailable ? "Werkstatt-Spielereien sind derzeit nicht verfügbar." : "");
        error.textContent = visibleMessage;
        error.hidden = !visibleMessage;
    }

    setAudioStatus(message: string): void {
        this.audioStatus = message;
        this.setError("");
    }

    animateAction(point: { x: number; y: number }, success: boolean): void {
        this.readout("axis-x", `${(point.x * 100).toFixed(1)} mm`);
        this.readout("axis-y", `${(point.y * 100).toFixed(1)} mm`);
        this.readout("axis-z", "12.0 mm");
        const angle = (point.x - 0.5) * 30;
        this.readout("angle", `${angle.toFixed(1)}°`);
        this.readout("axis-angle", `${angle.toFixed(1)}°`);
        this.root.style.setProperty("--carriage-position", `${20 + point.x * 45}%`);
        this.root.dataset.action = success ? "success" : "failure";
        clearTimeout(this.actionTimer);
        this.actionTimer = setTimeout(() => {
            delete this.root.dataset.action;
            this.readout("axis-z", "0.0 mm");
        }, 450);
    }

    destroy(): void {
        this.controller.abort();
        clearTimeout(this.eventTimer);
        clearTimeout(this.eventCooldownTimer);
        clearTimeout(this.actionTimer);
    }

    private icon(node: IconNode): SVGElement {
        const element = createElement(node);
        element.setAttribute("aria-hidden", "true");
        return element;
    }

    private makeButton(label: string, command: string, className: string): HTMLButtonElement {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.dataset.command = command;
        button.title = label;
        button.disabled = this.watcher;
        button.addEventListener("click", () => this.execute(command), { signal: this.controller.signal });
        this.commands.set(command, [...this.commands.get(command) ?? [], button]);
        return button;
    }

    private execute(command: string): void {
        if (this.watcher) return;
        this.setError("");
        if (command === "instructions") {
            this.demonstration = !this.demonstration;
            this.root.querySelector<HTMLElement>(".gesture-demo")!.hidden = !this.demonstration;
            this.setPressed(command, this.demonstration);
        } else if (command === "sound") {
            this.volume = (this.volume + 1) % 3;
            this.soundHandler([1, 0.3, 0][this.volume]);
            this.updateSoundButton();
        } else if (command === "start" || command === "laser" || command === "saw") {
            if (!this.state) return;
            this.commandHandler(command);
        }
    }

    private triggerEvent(button: HTMLButtonElement, effect: string): void {
        const options = this.eventOptions?.events[effect];
        if (this.watcher || !this.eventOptions?.enabled || !options?.enabled || button.disabled || this.root.dataset.effect) return;
        if ((this.eventCooldowns.get(effect) ?? 0) > performance.now()) return;
        this.eventCooldowns.set(effect, performance.now() + options.durationMilliseconds + options.cooldownMilliseconds);
        this.root.dataset.effect = effect;
        this.root.style.setProperty("--event-duration", `${options.durationMilliseconds}ms`);
        button.classList.add("is-active");
        this.refreshEventButtons();
        this.eventTimer = setTimeout(() => {
            delete this.root.dataset.effect;
            button.classList.remove("is-active");
            this.refreshEventButtons();
        }, options.durationMilliseconds);
    }

    private async loadEventOptions(): Promise<void> {
        try {
            const response = await fetch("/api/workshop-events", { signal: this.controller.signal, cache: "no-store" });
            if (!response.ok) throw new Error();
            const options: unknown = await response.json();
            if (typeof options !== "object" || options === null) throw new Error();
            const candidate = options as WorkshopEventOptions;
            if (typeof candidate.enabled !== "boolean" || typeof candidate.events !== "object" || candidate.events === null
                || Object.keys(candidate.events).length !== this.eventButtons.size
                || [...this.eventButtons.keys()].some(name => {
                    const event = candidate.events[name];
                    return !event || typeof event.enabled !== "boolean" || !Number.isInteger(event.durationMilliseconds)
                        || event.durationMilliseconds < 1 || event.durationMilliseconds > 2000
                        || !Number.isInteger(event.cooldownMilliseconds) || event.cooldownMilliseconds < 0 || event.cooldownMilliseconds > 60000;
                })) throw new Error();
            this.eventOptions = candidate;
            this.refreshEventButtons();
        } catch {
            if (!this.controller.signal.aborted) {
                this.eventConfigurationUnavailable = true;
                this.setError("");
            }
        }
    }

    private refreshEventButtons(): void {
        clearTimeout(this.eventCooldownTimer);
        const now = performance.now();
        let nextExpiration = Number.POSITIVE_INFINITY;
        for (const [name, button] of this.eventButtons) {
            const expiresAt = this.eventCooldowns.get(name) ?? 0;
            button.disabled = this.watcher || !this.eventOptions?.enabled || !this.eventOptions.events[name]?.enabled
                || Boolean(this.root.dataset.effect) || expiresAt > now;
            if (expiresAt > now) nextExpiration = Math.min(nextExpiration, expiresAt);
        }
        if (Number.isFinite(nextExpiration)) {
            this.eventCooldownTimer = setTimeout(() => this.refreshEventButtons(), Math.max(1, nextExpiration - now));
        }
    }

    private updateSoundButton(): void {
        const icon = [Volume2, Volume1, VolumeX][this.volume];
        const label = ["Ton: laut", "Ton: leise", "Ton: stumm"][this.volume];
        this.commands.get("sound")![0].replaceChildren(this.icon(icon), document.createTextNode("Ton"));
        for (const button of this.commands.get("sound")!) {
            button.title = label;
            button.setAttribute("aria-label", label);
        }
        this.setPressed("sound", this.volume !== 2);
        this.setEnabled("instructions", !this.watcher);
    }

    private readout(name: string, value: string): void {
        this.root.querySelector<HTMLElement>(`[data-readout="${name}"]`)!.textContent = value;
    }

    private setEnabled(command: string, enabled: boolean): void {
        for (const button of this.commands.get(command) ?? []) button.disabled = this.watcher || !enabled;
    }

    private setPressed(command: string, pressed: boolean): void {
        for (const button of this.commands.get(command) ?? []) button.setAttribute("aria-pressed", String(pressed));
    }
}