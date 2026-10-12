import {
    HubConnection,
    HubConnectionBuilder,
    HubConnectionState
} from "@microsoft/signalr";
import Phaser from "phaser";

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
};
type GestureSample = { x: number; y: number; elapsedMs: number };
type ActiveGesture = { startedAt: number; samples: GestureSample[] };

const gameWidth = 1280;
const gameHeight = 720;
const stageArea = { x: 64, y: 168, width: 1152, height: 454 };

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
    private stageGraphics!: Phaser.GameObjects.Graphics;
    private hudGraphics!: Phaser.GameObjects.Graphics;
    private titleText!: Phaser.GameObjects.Text;
    private statusText!: Phaser.GameObjects.Text;
    private scoreText!: Phaser.GameObjects.Text;
    private timerText!: Phaser.GameObjects.Text;
    private comboText!: Phaser.GameObjects.Text;
    private roleText!: Phaser.GameObjects.Text;
    private modeText!: Phaser.GameObjects.Text;
    private errorText!: Phaser.GameObjects.Text;
    private laserModeButton!: Phaser.GameObjects.Text;
    private sawModeButton!: Phaser.GameObjects.Text;
    private startButton!: Phaser.GameObjects.Text;
    private touchFeedback?: Phaser.GameObjects.Arc;

    constructor(requestedRole: ClientRole) {
        super({ key: `workshop-${requestedRole}` });
        this.requestedRole = requestedRole;
        this.clientInstanceId = crypto.randomUUID();
        this.connection = new HubConnectionBuilder()
            .withUrl("/gameHub")
            .withAutomaticReconnect()
            .build();
    }

    preload(): void {
        this.load.image("showroom", "/Assets/images/background/bg_0001.webp");
        for (const assetId of ["tool_0002", "tool_0006", "tool_0010"]) {
            this.load.image(assetId, `/Assets/images/tools/${assetId}.webp`);
        }
    }

    create(): void {
        this.cameras.main.setBackgroundColor("#292f31");
        this.createBackdrop();
        this.createInterface();
        this.registerInput();
        this.connection.on("SnapshotUpdated", (snapshot: GameSnapshot) => this.applySnapshot(snapshot));
        this.connection.on("ActionResolved", (action: ActionResolved) => this.showActionFeedback(action));
        this.connection.onreconnected(() => this.joinHub());
        void this.joinHub();
    }

    update(): void {
        if (this.snapshot?.phase !== "playing") {
            return;
        }

        const remainingMs = Math.max(0, this.snapshot.remainingMs - (performance.now() - this.snapshotReceivedAt));
        this.timerText.setText(`${Math.ceil(remainingMs / 1000)}`);
    }

    private createBackdrop(): void {
        const hasShowroom = this.textures.exists("showroom");
        if (hasShowroom) {
            this.add.image(gameWidth / 2, gameHeight / 2, "showroom")
                .setDisplaySize(gameWidth, gameHeight);
        }
        const graphics = this.add.graphics();
        if (!hasShowroom) {
            graphics.fillStyle(0x292f31, 1).fillRect(0, 0, gameWidth, gameHeight);
            graphics.fillStyle(0xc76b16, 1).fillRoundedRect(stageArea.x, stageArea.y, stageArea.width, stageArea.height, 6);
        }
        graphics.fillStyle(0x202628, 1).fillRect(0, 0, gameWidth, 142);
        graphics.fillStyle(0x394145, 1).fillRect(0, 142, gameWidth, 26);
        graphics.lineStyle(2, 0xf1a34c, 0.55).strokeRoundedRect(stageArea.x, stageArea.y, stageArea.width, stageArea.height, 6);
        for (let x = stageArea.x + 48; !hasShowroom && x < stageArea.x + stageArea.width; x += 96) {
            graphics.lineStyle(1, 0xffd092, 0.08).lineBetween(x, stageArea.y, x, stageArea.y + stageArea.height);
        }
        for (let y = stageArea.y + 46; !hasShowroom && y < stageArea.y + stageArea.height; y += 92) {
            graphics.lineStyle(1, 0xffd092, 0.08).lineBetween(stageArea.x, y, stageArea.x + stageArea.width, y);
        }
        graphics.fillStyle(0xe7d357, 0.86).fillRect(64, 153, 1152, 5);
        graphics.fillStyle(0x202628, 1).fillRect(0, 636, gameWidth, 84);
        graphics.lineStyle(2, 0x566165, 0.8).lineBetween(0, 636, gameWidth, 636);

        this.add.text(64, 24, "VOLLMER  /  WERKSTATT-SIMULATOR", {
            color: "#f2f0e8",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "24px",
            fontStyle: "bold"
        });
        this.add.text(65, 62, "PRÄZISION TRIFFT AUF SCHLEIFSTAUB", {
            color: "#aeb8ba",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "13px"
        });
        this.add.text(980, 24, hasShowroom ? "ARTWORK · PROTOTYP" : "PROTOTYP · PROZEDURAL", {
            color: "#fff0d8",
            backgroundColor: "#743d15",
            padding: { x: 9, y: 6 },
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "12px",
            fontStyle: "bold"
        });
        this.add.text(84, 180, "SHOWROOM", {
            color: "#fff0d8",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "12px",
            fontStyle: "bold"
        });
        this.add.text(84, 603, "PROTOTYP · TREFFERZONEN UND ZUSTANDSEFFEKTE NOCH NICHT FINAL", {
            color: "#4a2d18",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "11px",
            fontStyle: "bold"
        });

        this.stageGraphics = this.add.graphics().setDepth(20);
        this.hudGraphics = this.add.graphics().setDepth(30);
    }

    private createInterface(): void {
        this.scoreText = this.add.text(64, 104, "0000", this.hudStyle(25, "#ffffff", true)).setDepth(31);
        this.timerText = this.add.text(220, 104, "60", this.hudStyle(25, "#ffffff", true)).setDepth(31);
        this.comboText = this.add.text(294, 110, "COMBO 0", this.hudStyle(14, "#d4dcde", true));
        this.statusText = this.add.text(404, 111, "VERBINDE …", this.hudStyle(13, "#e4b333", true));
        this.roleText = this.add.text(64, 653, this.requestedRole === "player" ? "SPIELER" : "PASSIVER WATCHER", this.hudStyle(14, "#dbe0e0", true));
        this.modeText = this.add.text(390, 653, "F2 LASER     F3 SCHEIBE", this.hudStyle(15, "#dbe0e0", true));
        this.errorText = this.add.text(682, 653, "", this.hudStyle(13, "#ff8d85", false));
        this.titleText = this.add.text(640, 375, "BEREIT FÜR DEN NÄCHSTEN VERSUCH", {
            color: "#fff5e6",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "27px",
            fontStyle: "bold",
            align: "center",
            backgroundColor: "#763f16",
            padding: { x: 16, y: 12 }
        }).setOrigin(0.5).setDepth(25);

        this.laserModeButton = this.add.text(820, 74, "F2  LASER", this.hudStyle(16, "#d4fcff", true))
            .setInteractive({ useHandCursor: true })
            .on("pointerup", () => void this.selectMode("laser"));
        this.sawModeButton = this.add.text(940, 74, "F3  SCHEIBE", this.hudStyle(16, "#fff0cb", true))
            .setInteractive({ useHandCursor: true })
            .on("pointerup", () => void this.selectMode("saw"));
        this.startButton = this.add.text(1092, 68, "F1  START", {
            color: "#20170e",
            backgroundColor: "#ff9d25",
            fontFamily: "Segoe UI, sans-serif",
            fontSize: "18px",
            fontStyle: "bold",
            padding: { x: 17, y: 12 }
        }).setInteractive({ useHandCursor: true }).on("pointerup", () => void this.startRound());

        this.input.keyboard?.on("keydown-F1", () => void this.startRound());
        this.input.keyboard?.on("keydown-F2", () => void this.selectMode("laser"));
        this.input.keyboard?.on("keydown-F3", () => void this.selectMode("saw"));
        this.renderStatus();
    }

    private registerInput(): void {
        this.input.on("pointerdown", (pointer: Phaser.Input.Pointer) => {
            if (this.role !== "player" || this.snapshot?.phase !== "playing" || pointer.worldY < stageArea.y || pointer.worldY > stageArea.y + stageArea.height) {
                return;
            }

            const now = performance.now();
            this.activeGesture = {
                startedAt: now,
                samples: [this.toNormalizedPoint(pointer.worldX, pointer.worldY, now)]
            };
        });

        this.input.on("pointermove", (pointer: Phaser.Input.Pointer) => {
            if (this.activeGesture === null || !pointer.isDown || this.activeGesture.samples.length >= 32) {
                return;
            }

            this.activeGesture.samples.push(this.toNormalizedPoint(pointer.worldX, pointer.worldY, performance.now()));
        });

        this.input.on("pointerup", (pointer: Phaser.Input.Pointer) => {
            const gesture = this.activeGesture;
            this.activeGesture = null;
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
            this.statusText.setText(this.role === "player" ? "AKTIVER SPIELER" : "PASSIVER WATCHER");
            this.statusText.setColor(this.role === "player" ? "#a9df65" : "#9ed7e3");
            this.errorText.setText("");
        } catch (error) {
            this.statusText.setText("SERVER NICHT ERREICHBAR");
            this.statusText.setColor("#ff8d85");
            this.errorText.setText(error instanceof Error ? error.message.slice(0, 58) : "SignalR-Verbindung fehlgeschlagen");
        }
    }

    private applySnapshot(snapshot: GameSnapshot): void {
        if (this.snapshot !== null && snapshot.stateVersion < this.snapshot.stateVersion) {
            return;
        }

        this.role = this.requestedRole === "player" && snapshot.activeClientId === this.clientInstanceId
            ? "player"
            : "watcher";
        this.snapshot = snapshot;
        this.snapshotReceivedAt = performance.now();
        this.scoreText.setText(snapshot.score.toString().padStart(4, "0"));
        this.comboText.setText(`COMBO ${snapshot.combo}`);
        this.timerText.setText(`${Math.ceil(snapshot.remainingMs / 1000)}`);
        this.modeText.setText(snapshot.mode === "saw" ? "F2  LASER     F3  ◉ SCHEIBE" : "F2  ◉ LASER     F3  SCHEIBE");
        this.syncTargets(snapshot.tools);
        this.renderStatus();

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
        this.laserModeButton.setVisible(this.role === "player").setAlpha(canSelectMode && snapshot.mode === "laser" ? 1 : 0.55);
        this.sawModeButton.setVisible(this.role === "player").setAlpha(canSelectMode && snapshot.mode === "saw" ? 1 : 0.55);
        const canRequestRole = this.requestedRole === "player" && this.role === "watcher" && snapshot.phase !== "playing";
        const canStartRound = this.role === "player" && snapshot.phase !== "playing";
        this.startButton.setText(canRequestRole ? "F1  ÜBERNEHMEN" : "F1  START");
        this.startButton.setVisible(canRequestRole || canStartRound).setAlpha(canRequestRole || canStartRound ? 1 : 0.45);
        this.laserModeButton.disableInteractive();
        this.sawModeButton.disableInteractive();
        this.startButton.disableInteractive();
        if (canSelectMode) {
            this.laserModeButton.setInteractive({ useHandCursor: true });
            this.sawModeButton.setInteractive({ useHandCursor: true });
        }
        if (canRequestRole || canStartRound) {
            this.startButton.setInteractive({ useHandCursor: true });
        }
    }

    private syncTargets(tools: ToolSnapshot[]): void {
        const currentIds = new Set(tools.map(tool => tool.definition.toolId));
        for (const [toolId, container] of this.targetContainers) {
            if (!currentIds.has(toolId)) {
                container.destroy(true);
                this.targetContainers.delete(toolId);
            }
        }

        for (const tool of tools) {
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
            container.setDepth(40 + tool.definition.zOrder);
            container.setData("state", tool.state);
            this.drawTool(container, tool);
        }
    }

    private createToolView(tool: ToolSnapshot): Phaser.GameObjects.Container {
        const width = tool.definition.boundingWidthNormalized * gameWidth;
        const height = tool.definition.boundingHeightNormalized * gameHeight;
        const container = this.add.container(0, 0).setSize(width, height).setDepth(40 + tool.definition.zOrder);
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
        const graphics = container.getData("graphics") as Phaser.GameObjects.Graphics;
        const width = tool.definition.boundingWidthNormalized * gameWidth;
        const height = tool.definition.boundingHeightNormalized * gameHeight;
        const stateColors: Record<ToolState, { shaft: number; cut: number; edge: number }> = {
            dull: { shaft: 0x4a5960, cut: 0x7d9298, edge: 0x263237 },
            inProgress: { shaft: 0x56808c, cut: 0xffc653, edge: 0x263237 },
            sharp: { shaft: 0x427b4a, cut: 0xb1e05b, edge: 0x263237 },
            failed: { shaft: 0x672f32, cut: 0xeb625e, edge: 0x321e20 }
        };
        const colors = stateColors[tool.state];
        const artwork = container.getData("artwork") as Phaser.GameObjects.Image | undefined;
        graphics.clear();

        if (artwork) {
            const tint: Record<ToolState, number> = { dull: 0xffffff, inProgress: 0xffd27a, sharp: 0xb6ee8b, failed: 0xff7777 };
            artwork.setTint(tint[tool.state]);
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

        const label = tool.state === "dull" ? "STUMPF" : tool.state === "inProgress" ? "SCHLIFF" : tool.state === "sharp" ? "SCHARF" : "DEFEKT";
        const stateLabel = container.getByName("state-label") as Phaser.GameObjects.Text | null;
        if (stateLabel === null) {
            container.add(this.add.text(0, height * 0.5 + 3, label, {
                color: "#202426",
                backgroundColor: "#ffdda6",
                padding: { x: 4, y: 2 },
                fontFamily: "Segoe UI, sans-serif",
                fontSize: "11px",
                fontStyle: "bold"
            }).setOrigin(0.5).setName("state-label"));
        } else {
            stateLabel.setText(label);
        }
    }

    private async selectMode(mode: GameMode): Promise<void> {
        if (this.role !== "player" || this.snapshot?.phase === "playing") {
            return;
        }

        await this.invokeHub<boolean>("SelectMode", mode);
    }

    private async startRound(): Promise<void> {
        if (this.requestedRole !== "player" || this.snapshot?.phase === "playing") {
            return;
        }

        if (this.role !== "player") {
            await this.invokeHub<boolean>("RequestPlayerRole");
            return;
        }

        await this.invokeHub<boolean>("StartRound");
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
        if (this.touchFeedback !== undefined) {
            this.touchFeedback.destroy();
        }

        const color = action.outcome === "success" ? 0x94e200 : 0xfe0e26;
        const feedbackPoint = this.lastGesturePoint ?? { x: 0.5, y: 0.5 };
        this.touchFeedback = this.add.circle(feedbackPoint.x * gameWidth, feedbackPoint.y * gameHeight, 26, color, 0.55)
            .setDepth(80);
        this.tweens.add({
            targets: this.touchFeedback,
            alpha: 0,
            scale: 2.4,
            duration: 260,
            onComplete: () => this.touchFeedback?.destroy()
        });
    }

    private renderStatus(): void {
        const phase = this.snapshot?.phase ?? "attract";
        this.hudGraphics.clear();
        this.hudGraphics.fillStyle(0x434d51, 1).fillRoundedRect(64, 94, 112, 44, 4);
        this.hudGraphics.fillStyle(0x434d51, 1).fillRoundedRect(192, 94, 70, 44, 4);
        this.hudGraphics.fillStyle(this.role === "player" ? 0x94e200 : 0x00bbfc, 1).fillCircle(397, 119, 5);
        this.scoreText.setText((this.snapshot?.score ?? 0).toString().padStart(4, "0"));
        this.timerText.setText(this.snapshot === null ? "60" : `${Math.ceil(this.snapshot.remainingMs / 1000)}`);
        this.comboText.setText(`COMBO ${this.snapshot?.combo ?? 0}`);
        this.modeText.setText(this.snapshot?.mode === "saw" ? "F2  LASER     F3  ◉ SCHEIBE" : "F2  ◉ LASER     F3  SCHEIBE");
        this.roleText.setText(this.role === "player" ? "SPIELER · TOUCH AKTIV" : "WATCHER · NUR ANZEIGE");
        this.modeText.setVisible(this.role === "player");

        if (phase === "playing" && this.role === "watcher") {
            this.statusText.setText("LIVE · PASSIV");
            this.statusText.setColor("#8fe3f1");
        }
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
            this.errorText.setText(error instanceof Error ? error.message.slice(0, 58) : "Aktion fehlgeschlagen");
            return undefined;
        }
    }

    private hudStyle(fontSize: number, color: string, bold: boolean): Phaser.Types.GameObjects.Text.TextStyle {
        return {
            color,
            fontFamily: "Segoe UI, sans-serif",
            fontSize: `${fontSize}px`,
            fontStyle: bold ? "bold" : "normal"
        };
    }
}

export function createGame(role: ClientRole): Phaser.Game {
    return new Phaser.Game({
        type: Phaser.AUTO,
        parent: "game",
        width: gameWidth,
        height: gameHeight,
        backgroundColor: "#292f31",
        scale: {
            mode: Phaser.Scale.FIT,
            autoCenter: Phaser.Scale.CENTER_BOTH
        },
        scene: [new WorkshopScene(role)]
    });
}