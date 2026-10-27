import {
    HubConnection,
    HubConnectionBuilder,
    HubConnectionState
} from "@microsoft/signalr";
import Phaser from "phaser";
import { MachineShell } from "./machine-shell";
import { GameAudio } from "./game-audio";
import { PlayerDialogs, type HighscoreEntry, type OwnHighscore } from "./player-dialogs";

type ClientRole = "player" | "watcher";
type GameMode = "laser" | "saw";
type GamePhase = "attract" | "playing" | "results";
type ToolState = "dull" | "inProgress" | "sharp" | "failed";
type Outcome = "success" | "wrong_sharpening" | "shaft_hit" | "miss";
type Point2 = { x: number; y: number };
type HitZone = { zoneId: string; role: "cut" | "shaft"; points: Point2[] };
type ToolDefinition = {
    toolId: string;
    zOrder: number;
    hitZones: HitZone[];
    cutDirectionDegrees: number;
    boundingWidthNormalized: number;
    boundingHeightNormalized: number;
    visualKind: "drill" | "cutter" | "saw";
    hasPkd: boolean;
    assetId?: string | null;
};
type ToolPlacement = {
    center: Point2;
    velocityXPerSecond: number;
    velocityYPerSecond: number;
    rotationDegrees: number;
};
type ToolSnapshot = { definition: ToolDefinition; state: ToolState; placement: ToolPlacement | null };
type GameSnapshot = {
    roundId: string | null;
    stateVersion: number;
    phase: GamePhase;
    activeClientId: string | null;
    mode: GameMode;
    remainingMs: number;
    score: number;
    combo: number;
    roleVersion: number;
    tools: ToolSnapshot[];
    stageArea?: { left: number; top: number; width: number; height: number };
    wasAborted?: boolean;
};
type JoinResponse = {
    clientInstanceId: string;
    role: ClientRole;
    roleVersion: number;
    resumeToken: string;
    snapshot: GameSnapshot;
};
type ActionResolved = {
    roundId: string;
    eventId: string;
    outcome: Outcome;
    toolId: string | null;
    scoreDelta: number;
    score: number;
    combo: number;
    feedbackPoint?: Point2 | null;
    audioEvent: string;
};
type GestureSample = { x: number; y: number; elapsedMs: number };
type ActiveGesture = { startedAt: number; samples: GestureSample[] };

const gameWidth = 1280;
const gameHeight = 720;
const showroomImagePath = "/Assets/images/background/bg_0001.webp";
const stageArea = { x: 64, y: 201.6, width: 1152, height: 460.8 };

class WorkshopScene extends Phaser.Scene {
    private readonly requestedRole: ClientRole;
    private readonly connection: HubConnection;
    private readonly clientInstanceId: string;
    private resumeToken: string | undefined;
    private commandSequence = 0;
    private role: ClientRole = "watcher";
    private snapshot: GameSnapshot | null = null;
    private snapshotReceivedAt = performance.now();
    private activeGesture: ActiveGesture | null = null;
    private lastGesturePoint: Point2 | null = null;
    private targetContainers = new Map<string, Phaser.GameObjects.Container>();
    private titleText!: Phaser.GameObjects.Text;
    private touchFeedback?: Phaser.GameObjects.Arc;
    private inputArea = { ...stageArea };
    private gestureTrail!: Phaser.GameObjects.Graphics;
    private feedbackObjects = new Set<Phaser.GameObjects.GameObject>();
    private audio?: GameAudio;
    private readonly resolvedEvents = new Set<string>();
    private startingRound = false;
    private playerDialogs?: PlayerDialogs;
    private registeredRound = false;
    private readonly faceVariants: Record<ToolState, string[]> = { dull: [], inProgress: [], sharp: [], failed: [] };

    constructor(requestedRole: ClientRole, private readonly shell: MachineShell) {
        super({ key: `workshop-${requestedRole}` });
        this.requestedRole = requestedRole;
        this.clientInstanceId = crypto.randomUUID();
        this.connection = new HubConnectionBuilder()
            .withUrl("/gameHub")
            .withAutomaticReconnect()
            .build();
    }

    preload(): void {
        this.load.image("showroom", showroomImagePath);
        for (const assetId of ["tool_0002", "tool_0006", "tool_0010"]) {
            this.load.image(assetId, `/Assets/images/tools/${assetId}.webp`);
        }
        this.load.once("filecomplete-json-face-catalog", () => {
            const catalog = this.cache.json.get("face-catalog") as { version: number; states: Record<ToolState, string[]> };
            if (catalog?.version !== 1) return;
            for (const state of Object.keys(this.faceVariants) as ToolState[]) {
                for (const filename of catalog.states[state] ?? []) {
                    if (!/^face_[a-zA-Z0-9_]+\.webp$/.test(filename)) continue;
                    const key = filename.slice(0, -5);
                    this.faceVariants[state].push(key);
                    this.load.image(key, `/Assets/images/faces/${filename}`);
                }
            }
        });
        this.load.json("face-catalog", "/Assets/images/faces/catalog.json");
    }

    create(): void {
        this.createBackdrop();
        this.createInterface();
        this.gestureTrail = this.add.graphics().setDepth(75);
        this.registerInput();
        if (this.requestedRole === "player") this.audio = new GameAudio(message => this.shell.setAudioStatus(message));
        if (this.requestedRole === "player") {
            this.playerDialogs = new PlayerDialogs(async mode => {
                const entries = await this.invokeHub<HighscoreEntry[]>("GetHighscores", mode);
                if (!entries) throw new Error();
                return entries;
            }, async code => await this.invokeHub<boolean>("DeleteHighscore", code) === true);
        }
        const unlockAudio = () => {
            if (this.role === "player") void this.audio?.unlock();
        };
        if (this.audio) {
            document.addEventListener("pointerdown", unlockAudio);
            document.addEventListener("keydown", unlockAudio);
        }
        this.shell.bind(command => {
            if (command === "start") void this.startRound();
            else if (command === "highscore") void this.showLeaderboard();
            else void this.selectMode(command);
        }, volume => {
            this.audio?.setVolume(volume);
        });
        this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
            document.removeEventListener("pointerdown", unlockAudio);
            document.removeEventListener("keydown", unlockAudio);
            this.shell.destroy();
            this.playerDialogs?.destroy();
            this.audio?.destroy();
            void this.connection.stop();
        });
        this.connection.on("SnapshotUpdated", (snapshot: GameSnapshot) => this.applySnapshot(snapshot));
        this.connection.on("ActionResolved", (action: ActionResolved) => this.showActionFeedback(action));
        this.connection.onreconnecting(() => { this.audio?.setRound(null, false, false); this.shell.setConnection("VERBINDE …", true); });
        this.connection.onclose(() => { this.audio?.setRound(null, false, false); this.shell.setConnection("OFFLINE", true); });
        this.connection.onreconnected(() => this.joinHub());
        void this.joinHub();
    }

    update(): void {
        if (this.snapshot?.phase !== "playing") {
            return;
        }

        const remainingMs = Math.max(0, this.snapshot.remainingMs - (performance.now() - this.snapshotReceivedAt));
        this.shell.setTime(remainingMs);
    }

    private createBackdrop(): void {
        const hasShowroom = this.textures.exists("showroom");
        if (hasShowroom) {
            this.game.canvas.parentElement!.style.backgroundImage = `url("${showroomImagePath}")`;
        }
        const graphics = this.add.graphics();
        if (!hasShowroom) {
            graphics.fillStyle(0x292f31, 1).fillRect(0, 0, gameWidth, gameHeight);
            graphics.fillStyle(0xc76b16, 1).fillRoundedRect(stageArea.x, stageArea.y, stageArea.width, stageArea.height, 6);
        }
        for (let x = stageArea.x + 48; !hasShowroom && x < stageArea.x + stageArea.width; x += 96) {
            graphics.lineStyle(1, 0xffd092, 0.08).lineBetween(x, stageArea.y, x, stageArea.y + stageArea.height);
        }
        for (let y = stageArea.y + 46; !hasShowroom && y < stageArea.y + stageArea.height; y += 92) {
            graphics.lineStyle(1, 0xffd092, 0.08).lineBetween(stageArea.x, y, stageArea.x + stageArea.width, y);
        }
    }

    private createInterface(): void {
        this.titleText = this.add.text(640, 375, "BEREIT FÜR DEN NÄCHSTEN VERSUCH", {
            color: "#fff5e6",
            fontFamily: "Bahnschrift, sans-serif",
            fontSize: "27px",
            fontStyle: "bold",
            align: "center",
            backgroundColor: "#763f16",
            padding: { x: 16, y: 12 }
        }).setOrigin(0.5).setDepth(90);

    }

    private registerInput(): void {
        const cancelGesture = () => { this.activeGesture = null; this.gestureTrail.clear(); };
        this.input.on("pointerupoutside", cancelGesture);
        this.game.canvas.addEventListener("pointercancel", cancelGesture);
        this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => this.game.canvas.removeEventListener("pointercancel", cancelGesture));
        this.input.on("pointerdown", (pointer: Phaser.Input.Pointer) => {
            if (this.role !== "player" || this.snapshot?.phase !== "playing" || pointer.worldX < this.inputArea.x || pointer.worldX > this.inputArea.x + this.inputArea.width || pointer.worldY < this.inputArea.y || pointer.worldY > this.inputArea.y + this.inputArea.height) {
                return;
            }

            const now = performance.now();
            this.gestureTrail.clear();
            const impulse = this.trackFeedback(this.add.circle(pointer.worldX, pointer.worldY, 10, 0x00bbfc, 0.35).setDepth(76));
            this.tweens.add({ targets: impulse, alpha: 0, scale: 1.8, duration: 150, onComplete: () => impulse.destroy() });
            this.activeGesture = {
                startedAt: now,
                samples: [this.toNormalizedPoint(pointer.worldX, pointer.worldY, now)]
            };
        });

        this.input.on("pointermove", (pointer: Phaser.Input.Pointer) => {
            if (this.activeGesture === null || !pointer.isDown || this.activeGesture.samples.length >= 31) {
                return;
            }

            const previous = this.activeGesture.samples.at(-1)!;
            const point = this.toNormalizedPoint(pointer.worldX, pointer.worldY, performance.now());
            this.gestureTrail.lineStyle(3, this.snapshot?.mode === "saw" ? 0xffc653 : 0x00bbfc, 0.9)
                .lineBetween(previous.x * gameWidth, previous.y * gameHeight, point.x * gameWidth, point.y * gameHeight);
            this.activeGesture.samples.push(point);
        });

        this.input.on("pointerup", (pointer: Phaser.Input.Pointer) => {
            const gesture = this.activeGesture;
            this.activeGesture = null;
            this.gestureTrail.clear();
            if (gesture === null || this.snapshot?.phase !== "playing") {
                return;
            }

            const end = this.toNormalizedPoint(pointer.worldX, pointer.worldY, performance.now());
            const start = gesture.samples[0];
            const distance = Phaser.Math.Distance.Between(start.x, start.y, end.x, end.y);
            const isTap = distance < 0.018;
            this.lastGesturePoint = { x: end.x, y: end.y };
            const points = isTap ? [{ x: end.x, y: end.y }] : [...gesture.samples, end]
                .filter((point, index, items) => index === 0 || Phaser.Math.Distance.Between(point.x, point.y, items[index - 1].x, items[index - 1].y) > 0.004)
                .map(point => ({ x: point.x, y: point.y }));
            void this.submitAction(isTap ? "tap" : "swipe", points, gesture.startedAt, end.elapsedMs);
        });
    }

    private async joinHub(): Promise<void> {
        try {
            if (this.connection.state !== HubConnectionState.Connected) {
                await this.connection.start();
            }

            const response = await this.connection.invoke<JoinResponse>("JoinGame", {
                clientInstanceId: this.clientInstanceId,
                requestedRole: this.requestedRole,
                resumeToken: this.resumeToken ?? null
            });
            this.role = response.role;
            this.resumeToken = response.resumeToken;
            this.applySnapshot(response.snapshot);
            this.shell.setError("");
        } catch (error) {
            this.shell.setConnection("OFFLINE", true);
            this.shell.setError(error instanceof Error ? error.message.slice(0, 100) : "SignalR-Verbindung fehlgeschlagen");
        }
    }

    private applySnapshot(snapshot: GameSnapshot): void {
        if (this.snapshot !== null && snapshot.stateVersion < this.snapshot.stateVersion) {
            return;
        }

        const roundEnded = this.snapshot?.phase === "playing" && snapshot.phase === "results";
        this.role = this.requestedRole === "player" && snapshot.activeClientId === this.clientInstanceId
            ? "player"
            : "watcher";
        if (this.role !== "player") this.playerDialogs?.close();
        if (this.snapshot?.roundId !== snapshot.roundId || snapshot.phase !== "playing" || this.role !== "player") {
            this.activeGesture = null;
            this.gestureTrail.clear();
        }
        if (this.snapshot?.roundId !== snapshot.roundId || snapshot.phase !== "playing") {
            for (const effect of this.feedbackObjects) { this.tweens.killTweensOf(effect); effect.destroy(); }
            this.feedbackObjects.clear();
            this.touchFeedback = undefined;
        }
        if (this.snapshot?.roundId !== snapshot.roundId) this.resolvedEvents.clear();
        this.audio?.setRound(snapshot.roundId, this.role === "player", snapshot.phase === "playing");
        this.snapshot = snapshot;
        if (snapshot.stageArea) {
            this.inputArea = { x: snapshot.stageArea.left * gameWidth, y: snapshot.stageArea.top * gameHeight,
                width: snapshot.stageArea.width * gameWidth, height: snapshot.stageArea.height * gameHeight };
        }
        this.snapshotReceivedAt = performance.now();
        this.syncTargets(snapshot.tools);

        if (snapshot.phase === "attract") {
            this.titleText.setText(this.getAttractMessage());
            this.titleText.setVisible(true);
        } else if (snapshot.phase === "results") {
            this.titleText.setText(this.getResultsMessage(snapshot.score));
            this.titleText.setVisible(true);
        } else {
            this.titleText.setVisible(false);
        }

        const canSelectMode = this.role === "player" && snapshot.phase !== "playing";
        const canRequestRole = this.requestedRole === "player" && this.role === "watcher" && snapshot.phase !== "playing";
        const canStartRound = this.role === "player" && snapshot.phase !== "playing";
        this.shell.update({ ...snapshot, canStart: canStartRound, canSelectMode, canRequestRole,
            activePlayer: this.role === "player", targetCount: snapshot.tools.length });
        if (roundEnded && !snapshot.wasAborted && this.role === "player") void this.showLeaderboard(true);
    }

    private syncTargets(tools: ToolSnapshot[]): void {
        const currentIds = new Set(tools.map(tool => tool.definition.toolId));
        for (const [toolId, container] of this.targetContainers) {
            if (!currentIds.has(toolId)) {
                container.destroy(true);
                this.targetContainers.delete(toolId);
            }
        }

        const orderedTools = [...tools].sort((first, second) => first.definition.zOrder - second.definition.zOrder);
        for (const [layer, tool] of orderedTools.entries()) {
            if (tool.placement === null) {
                continue;
            }

            let container = this.targetContainers.get(tool.definition.toolId);
            if (container === undefined) {
                container = this.createToolView(tool);
                this.targetContainers.set(tool.definition.toolId, container);
            }

            container.setPosition(
                tool.placement.center.x * gameWidth,
                tool.placement.center.y * gameHeight);
            container.setAngle(tool.placement.rotationDegrees);
            container.setDepth(40 + layer);
            container.setData("state", tool.state);
            this.drawTool(container, tool);
        }
    }

    private createToolView(tool: ToolSnapshot): Phaser.GameObjects.Container {
        const width = tool.definition.boundingWidthNormalized * gameWidth;
        const height = tool.definition.boundingHeightNormalized * gameHeight;
        const container = this.add.container(0, 0).setSize(width, height).setDepth(40);
        const graphics = this.add.graphics();
        container.add(graphics);
        container.setData("graphics", graphics);
        const assetId = tool.definition.assetId;
        if (assetId && this.textures.exists(assetId)) {
            const artwork = this.add.image(0, 0, assetId).setDisplaySize(width, height);
            container.addAt(artwork, 0);
            container.setData("artwork", artwork);
        }
        return container;
    }

    private drawTool(container: Phaser.GameObjects.Container, tool: ToolSnapshot): void {
        const previous = container.getData("authoritative-state") as ToolState | undefined;
        if (previous === "dull" && (tool.state === "sharp" || tool.state === "failed")) {
            const duration = tool.state === "sharp" ? 300 : 150;
            container.setData("transition-until", performance.now() + duration);
            this.time.delayedCall(duration, () => {
                if (!container.active) return;
                container.setData("transition-until", 0);
                const latest = this.snapshot?.tools.find(target => target.definition.toolId === tool.definition.toolId);
                if (latest) this.drawTool(container, latest);
            });
        }
        container.setData("authoritative-state", tool.state);
        const visualState: ToolState = (tool.state === "sharp" || tool.state === "failed")
            && Number(container.getData("transition-until") ?? 0) > performance.now() ? "inProgress" : tool.state;
        if (container.getData("rendered-state") === visualState) return;
        container.setData("rendered-state", visualState);
        const graphics = container.getData("graphics") as Phaser.GameObjects.Graphics;
        const width = tool.definition.boundingWidthNormalized * gameWidth;
        const height = tool.definition.boundingHeightNormalized * gameHeight;
        const stateColors: Record<ToolState, { shaft: number; cut: number; edge: number }> = {
            dull: { shaft: 0x4a5960, cut: 0x7d9298, edge: 0x263237 },
            inProgress: { shaft: 0x56808c, cut: 0xffc653, edge: 0x263237 },
            sharp: { shaft: 0x427b4a, cut: 0xb1e05b, edge: 0x263237 },
            failed: { shaft: 0x672f32, cut: 0xeb625e, edge: 0x321e20 }
        };
        const colors = stateColors[visualState];
        const artwork = container.getData("artwork") as Phaser.GameObjects.Image | undefined;
        graphics.clear();

        if (artwork) {
            artwork.clearTint().setTexture(this.stateTexture(tool.definition, visualState)).setDisplaySize(width, height);
        }
        for (const zone of artwork ? [] : tool.definition.hitZones) {
            const polygon = zone.points.map(point => ({
                x: (point.x - 0.5) * width,
                y: (point.y - 0.5) * height
            }));
            graphics.fillStyle(zone.role === "shaft" ? colors.shaft : colors.cut, 1);
            graphics.lineStyle(3, colors.edge, 1);
            graphics.fillPoints(polygon, true);
            graphics.strokePoints(polygon, true);
        }

        if (!artwork && tool.definition.visualKind === "saw") {
            graphics.lineStyle(2, 0xf7e7bd, 0.75).strokeCircle(width * 0.2, 0, height * 0.28);
            graphics.lineStyle(1, 0x252a2c, 0.72).lineBetween(-width * 0.18, 0, -width * 0.05, 0);
        } else if (!artwork) {
            graphics.lineStyle(2, 0xe2e8e7, 0.75).lineBetween(-width * 0.28, -height * 0.1, -width * 0.28, height * 0.1);
            graphics.lineStyle(2, 0xe2e8e7, 0.75).lineBetween(-width * 0.12, -height * 0.1, -width * 0.12, height * 0.1);
        }

        if (!artwork && tool.definition.hasPkd && tool.state === "dull") {
            graphics.fillStyle(0x52dcf5, 1).fillCircle(width * 0.27, -height * 0.15, 5);
            graphics.fillStyle(0x52dcf5, 1).fillCircle(width * 0.34, -height * 0.15, 5);
        }

        this.drawImageFace(container, tool.definition, visualState, width, height);
    }

    private drawImageFace(container: Phaser.GameObjects.Container, definition: ToolDefinition, state: ToolState, width: number, height: number): void {
        const variants = this.faceVariants[state].filter(key => this.textures.exists(key));
        let face = container.getByName("face-overlay") as Phaser.GameObjects.Image | null;
        if (variants.length === 0) { face?.setVisible(false); return; }
        let seed = 2166136261;
        for (const character of definition.toolId) seed = Math.imul(seed ^ character.charCodeAt(0), 16777619);
        const key = variants[(seed >>> 0) % variants.length];
        if (!face) {
            face = this.add.image(0, 0, key).setName("face-overlay");
            container.add(face);
        }
        face.setTexture(key).setVisible(true);
        const image = this.textures.get(key).getSourceImage() as HTMLImageElement;
        const displayWidth = width * (definition.visualKind === "saw" ? 0.42 : 0.32);
        const displayHeight = displayWidth * image.height / image.width;
        face.setDisplaySize(displayWidth, displayHeight)
            .setPosition(definition.visualKind === "saw" ? 0 : -width * 0.11, definition.visualKind === "saw" ? -height * 0.17 : 0);
    }

    private stateTexture(definition: ToolDefinition, state: ToolState): string {
        const assetId = definition.assetId!;
        const key = `${assetId}-state-${state}`;
        if (this.textures.exists(key)) return key;
        const source = this.textures.get(assetId).getSourceImage() as HTMLImageElement | HTMLCanvasElement;
        const texture = this.textures.createCanvas(key, source.width, source.height);
        if (!texture) return assetId;
        const context = texture.context;
        context.drawImage(source, 0, 0);
        const polygon = (zone: HitZone) => {
            const path = new Path2D();
            for (const [index, point] of zone.points.entries()) {
                if (index === 0) path.moveTo(point.x * source.width, point.y * source.height);
                else path.lineTo(point.x * source.width, point.y * source.height);
            }
            path.closePath();
            return path;
        };
        const cut = definition.hitZones.find(zone => zone.role === "cut");
        if (cut) {
            context.save();
            context.clip(polygon(cut));
            const withoutShaft = new Path2D();
            withoutShaft.rect(0, 0, source.width, source.height);
            for (const shaft of definition.hitZones.filter(zone => zone.role === "shaft")) withoutShaft.addPath(polygon(shaft));
            context.clip(withoutShaft, "evenodd");
            context.globalCompositeOperation = "source-atop";
            const fills: Record<ToolState, string> = { dull: "rgba(66,79,85,0.50)", inProgress: "rgba(255,185,35,0.72)",
                sharp: "rgba(148,226,0,0.55)", failed: "rgba(235,42,53,0.78)" };
            context.fillStyle = fills[state];
            context.fillRect(0, 0, source.width, source.height);
            context.strokeStyle = state === "failed" ? "rgba(35,18,22,0.9)" : state === "dull" ? "rgba(32,43,48,0.7)" : "rgba(255,255,232,0.92)";
            context.lineWidth = Math.max(2, source.width / (state === "failed" ? 140 : 250));
            for (let offset = -source.height; offset < source.width; offset += Math.max(18, source.width / 28)) {
                context.beginPath();
                context.moveTo(offset, 0);
                context.lineTo(offset + source.height, source.height);
                context.stroke();
            }
            context.restore();
        }
        const shaft = definition.hitZones.find(zone => zone.role === "shaft");
        if (shaft && !this.faceVariants[state].some(key => this.textures.exists(key))) {
            context.save();
            context.globalCompositeOperation = "source-over";
            this.drawFace(context, source.width, source.height, state, definition.visualKind);
            context.restore();
        }
        texture.refresh();
        return key;
    }

    private drawFace(context: CanvasRenderingContext2D, width: number, height: number, state: ToolState, kind: string): void {
        const size = Math.min(width * 0.23, height * 0.65);
        const centerHorizontal = width * (kind === "saw" ? 0.5 : 0.39);
        let centerVertical = height * (kind === "saw" ? 0.33 : 0.5);
        let verticalScale = 1;
        if (kind !== "saw") {
            const column = context.getImageData(Math.floor(centerHorizontal), 0, 1, height).data;
            let top = height;
            let bottom = -1;
            for (let vertical = 0; vertical < height; vertical++) {
                if (column[vertical * 4 + 3] < 128) continue;
                top = Math.min(top, vertical);
                bottom = Math.max(bottom, vertical);
            }
            if (bottom >= top) {
                const bodyHeight = bottom - top + 1;
                centerVertical = top + bodyHeight * 0.45;
                verticalScale = Math.min(1, bodyHeight / (size * 0.9));
            }
        }
        context.translate(centerHorizontal, centerVertical);
        context.scale(2, verticalScale * 2);
        context.lineWidth = Math.max(3, size * 0.045);
        context.lineCap = "round";
        context.strokeStyle = "#182225";
        const eyeVertical = -size * 0.12;
        for (const direction of [-1, 1]) {
            const horizontal = direction * size * 0.24;
            context.fillStyle = "#fffdf1";
            context.beginPath();
            context.ellipse(horizontal, eyeVertical, size * 0.16, size * 0.20, direction * -0.12, 0, Math.PI * 2);
            context.fill();
            context.stroke();
            context.fillStyle = "#182225";
            if (state === "failed") {
                context.beginPath();
                context.moveTo(horizontal - size * 0.07, eyeVertical - size * 0.07);
                context.lineTo(horizontal + size * 0.07, eyeVertical + size * 0.07);
                context.moveTo(horizontal + size * 0.07, eyeVertical - size * 0.07);
                context.lineTo(horizontal - size * 0.07, eyeVertical + size * 0.07);
                context.stroke();
            } else {
                context.beginPath();
                context.arc(horizontal + size * 0.025, eyeVertical + (state === "inProgress" ? size * 0.06 : 0), size * 0.055, 0, Math.PI * 2);
                context.fill();
            }
            if (state === "dull") {
                context.fillStyle = "#9eabb0";
                context.beginPath();
                context.ellipse(horizontal, eyeVertical - size * 0.025, size * 0.15, size * 0.13, 0, Math.PI, Math.PI * 2);
                context.fill();
            }
            context.beginPath();
            context.moveTo(horizontal - size * 0.16, eyeVertical - size * 0.25 + (state === "dull" ? direction * size * 0.04 : 0));
            context.lineTo(horizontal + size * 0.16, eyeVertical - size * 0.25 - (state === "dull" ? direction * size * 0.04 : 0));
            context.stroke();
        }
        context.fillStyle = "#202428";
        context.beginPath();
        if (state === "sharp") {
            context.moveTo(-size * 0.23, size * 0.16);
            context.quadraticCurveTo(0, size * 0.53, size * 0.23, size * 0.16);
            context.closePath();
            context.fill();
            context.stroke();
            context.fillStyle = "#fffdf1";
            context.fillRect(-size * 0.13, size * 0.18, size * 0.26, size * 0.07);
        } else if (state === "inProgress" || state === "failed") {
            context.ellipse(0, size * 0.26, size * (state === "failed" ? 0.16 : 0.09), size * 0.14, 0, 0, Math.PI * 2);
            context.fill();
            context.stroke();
            if (state === "failed") {
                context.fillStyle = "#f27682";
                context.beginPath();
                context.ellipse(size * 0.05, size * 0.35, size * 0.10, size * 0.05, 0, 0, Math.PI * 2);
                context.fill();
            }
        } else {
            context.moveTo(-size * 0.17, size * 0.28);
            context.quadraticCurveTo(0, size * 0.18, size * 0.17, size * 0.28);
            context.stroke();
        }
    }

    private async selectMode(mode: GameMode): Promise<void> {
        if (this.role !== "player" || this.snapshot?.phase === "playing") {
            return;
        }

        await this.invokeHub<boolean>("SelectMode", mode);
    }

    private async startRound(): Promise<void> {
        if (this.requestedRole !== "player" || this.startingRound) {
            return;
        }

        if (this.role !== "player") {
            if (this.snapshot?.phase !== "playing") await this.invokeHub<boolean>("RequestPlayerRole");
            return;
        }

        this.startingRound = true;
        try {
            if (this.snapshot?.phase === "playing") {
                if (this.snapshot.roundId) await this.invokeHub<boolean>("AbortRound", this.snapshot.roundId);
                return;
            }
            const registration = await this.playerDialogs?.requestRegistration();
            if (registration === undefined || !this.canBeginRound()) return;
            await this.audio?.unlock();
            if (this.canBeginRound()) {
                const accepted = registration === null
                    ? await this.invokeHub<boolean>("StartRound")
                    : await this.invokeHub<boolean>("StartRegisteredRound", registration);
                this.registeredRound = accepted === true && registration !== null;
            }
        } finally { this.startingRound = false; }
    }

    private async showLeaderboard(afterRound = false): Promise<void> {
        if (!this.playerDialogs || !this.snapshot || this.startingRound) return;
        const snapshot = this.snapshot;
        const own = this.role === "player" && snapshot.roundId && snapshot.phase === "results" && !snapshot.wasAborted
            ? await this.invokeHub<OwnHighscore | null>("GetOwnHighscore", snapshot.roundId) : undefined;
        if (this.snapshot?.roundId !== snapshot.roundId || this.snapshot?.phase !== snapshot.phase
            || this.snapshot?.activeClientId !== snapshot.activeClientId || this.snapshot?.mode !== snapshot.mode) return;
        this.playerDialogs.showLeaderboard(snapshot.mode, own ?? undefined,
            afterRound && this.registeredRound && !own ? "Die Speicherung deines Ergebnisses konnte noch nicht bestätigt werden." : "");
    }

    private canBeginRound(): boolean {
        return this.role === "player" && this.snapshot?.phase !== "playing";
    }

    private getAttractMessage(): string {
        if (this.requestedRole === "player" && this.role === "watcher") {
            return "F1 SPIEL ÜBERNEHMEN  ·  NUR AUSSERHALB EINER RUNDE";
        }

        return this.role === "player" ? "F1 START  ·  WÄHLE DEIN WERKZEUG" : "ZUSCHAUERANSICHT  ·  START AM TOUCHDISPLAY";
    }

    private getResultsMessage(score: number): string {
        if (this.requestedRole === "player" && this.role === "watcher") {
            return `RUNDE BEENDET · ${score} PUNKTE · F1 ZUM SPIEL ÜBERNEHMEN`;
        }

        return `RUNDE BEENDET  ·  ${score} PUNKTE`;
    }

    private async submitAction(kind: "tap" | "swipe", points: Point2[], startedAt: number, endedAt: number): Promise<void> {
        if (this.role !== "player" || this.snapshot?.phase !== "playing" || this.snapshot.roundId === null) {
            return;
        }

        this.commandSequence++;
        const request = {
            clientInstanceId: this.clientInstanceId,
            commandSequence: this.commandSequence,
            roundId: this.snapshot.roundId,
            toolId: this.findNearestTool(points[0])?.definition.toolId ?? "no-target",
            inputTimestamp: new Date().toISOString(),
            gesture: {
                kind,
                points,
                startedAtMs: Math.max(0, Math.round(startedAt - this.snapshotReceivedAt)),
                endedAtMs: Math.max(0, Math.round(endedAt - this.snapshotReceivedAt))
            }
        };

        await this.invokeHub("SubmitAction", request);
    }

    private findNearestTool(point: Point2): ToolSnapshot | undefined {
        const eligibleTools = this.snapshot?.tools.filter(tool => tool.state === "dull" && tool.placement !== null);
        return eligibleTools?.sort((first, second) => this.distanceToTool(point, first) - this.distanceToTool(point, second))[0];
    }

    private distanceToTool(point: Point2, tool: ToolSnapshot): number {
        const center = tool.placement!.center;
        return Phaser.Math.Distance.Between(point.x, point.y, center.x, center.y);
    }

    private showActionFeedback(action: ActionResolved): void {
        if (action.roundId !== this.snapshot?.roundId) return;
        if (this.resolvedEvents.has(action.eventId)) return;
        this.resolvedEvents.add(action.eventId);
        if (this.resolvedEvents.size > 1024) this.resolvedEvents.delete(this.resolvedEvents.values().next().value!);
        this.audio?.play(action.eventId, action.roundId, action.outcome === "miss" ? null : action.audioEvent,
            this.snapshot.mode === "saw" ? "grind_effect" : "laser_effect");
        if (this.touchFeedback !== undefined) {
            this.touchFeedback.destroy();
        }

        const color = action.outcome === "success" ? 0x94e200 : 0xfe0e26;
        const target = this.snapshot?.tools.find(tool => tool.definition.toolId === action.toolId);
        const feedbackPoint = action.feedbackPoint ?? this.lastGesturePoint ?? target?.placement?.center ?? { x: 0.5, y: 0.5 };
        this.shell.animateAction(feedbackPoint, action.outcome === "success");
        const feedback = this.trackFeedback(this.add.circle(feedbackPoint.x * gameWidth, feedbackPoint.y * gameHeight, 26, color, 0.55)
            .setDepth(80));
        this.touchFeedback = feedback;
        this.tweens.add({
            targets: feedback,
            alpha: 0,
            scale: 2.4,
            duration: 260,
            onComplete: () => feedback.destroy()
        });
        const labels: Record<Outcome, string> = { success: `+${action.scoreDelta}`, shaft_hit: `SCHAFT ${action.scoreDelta}`,
            wrong_sharpening: `FALSCH ${action.scoreDelta}`, miss: `DANEBEN ${action.scoreDelta}` };
        const text = this.trackFeedback(this.add.text(Phaser.Math.Clamp(feedbackPoint.x * gameWidth, 90, gameWidth - 90),
            Phaser.Math.Clamp(feedbackPoint.y * gameHeight - 30, 24, gameHeight - 30), labels[action.outcome],
            { fontFamily: "Bahnschrift, sans-serif", fontSize: "22px", fontStyle: "bold", color: action.outcome === "success" ? "#b6ff72" : "#ff8894",
                stroke: "#172225", strokeThickness: 4 }).setOrigin(0.5).setDepth(81));
        this.tweens.add({ targets: text, y: text.y - 18, alpha: 0, duration: 550, onComplete: () => text.destroy() });
    }

    private trackFeedback<T extends Phaser.GameObjects.GameObject>(effect: T): T {
        this.feedbackObjects.add(effect);
        effect.once(Phaser.GameObjects.Events.DESTROY, () => this.feedbackObjects.delete(effect));
        return effect;
    }

    private toNormalizedPoint(x: number, y: number, elapsedMs: number): GestureSample {
        return {
            x: Phaser.Math.Clamp(x / gameWidth, 0, 1),
            y: Phaser.Math.Clamp(y / gameHeight, 0, 1),
            elapsedMs
        };
    }

    private async invokeHub<TResult = void>(methodName: string, ...args: unknown[]): Promise<TResult | undefined> {
        if (this.connection.state !== HubConnectionState.Connected) {
            return undefined;
        }

        try {
            return await this.connection.invoke<TResult>(methodName, ...args);
        } catch (error) {
            this.shell.setError(error instanceof Error ? error.message.slice(0, 100) : "Aktion fehlgeschlagen");
            return undefined;
        }
    }
}

export function createGame(role: ClientRole): Phaser.Game {
    const shell = new MachineShell(role === "watcher");
    const game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: "game-stage",
        width: gameWidth,
        height: gameHeight,
        transparent: true,
        audio: { noAudio: true },
        scale: {
            mode: Phaser.Scale.ENVELOP,
            autoCenter: Phaser.Scale.CENTER_BOTH
        },
        scene: [new WorkshopScene(role, shell)]
    });
    const stage = document.getElementById("game-stage")!;
    const observer = new ResizeObserver(entries => {
        const bounds = entries[0].contentRect;
        if (game.scale && bounds.width > 0 && bounds.height > 0) game.scale.setParentSize(bounds.width, bounds.height);
    });
    observer.observe(stage);
    game.events.once(Phaser.Core.Events.DESTROY, () => observer.disconnect());
    return game;
}