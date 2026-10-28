import type { BufferGeometry, Group, Material, PerspectiveCamera, Scene, WebGLRenderer, WebGLRenderTarget } from "three";

export class AttractScreen {
    private readonly root = document.createElement("div");
    private readonly reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    private readonly observer: ResizeObserver;
    private renderer?: WebGLRenderer;
    private scene?: Scene;
    private camera?: PerspectiveCamera;
    private emblem?: Group;
    private geometry?: BufferGeometry;
    private materials: Material[] = [];
    private environment?: WebGLRenderTarget;
    private frame?: number;
    private active = false;
    private demo = false;
    private readonly demoStatus = document.createElement("div");
    private initializing = false;
    private destroyed = false;
    private failed = false;

    constructor(stage: HTMLElement) {
        this.root.className = "attract-screen";
        this.root.hidden = true;
        this.root.setAttribute("role", "group");
        this.root.setAttribute("aria-label", "VSharp Startbildschirm");
        this.root.dataset.renderer = "loading";
        const logo = document.createElement("img");
        logo.className = "attract-logo";
        logo.src = "/Assets/images/ui/vollmer-logo.png";
        logo.alt = "VOLLMER";
        const title = document.createElement("h1");
        title.className = "attract-title";
        title.textContent = "VSharp";
        const information = document.createElement("address");
        information.className = "attract-information";
        information.innerHTML = `
            <div><strong>VOLLMER WERKE</strong><span>Maschinenfabrik GmbH</span><span>Postfach 1760</span><span>D-88396 Biberach/Riß</span></div>
            <div><span>Telefon +49 (0)7351/571-0</span><span>Fax +49 (0)7351/571-130</span><span>info@vollmer-group.com</span><span>www.vollmer-group.com</span></div>`;
        const prompt = document.createElement("div");
        prompt.className = "attract-prompt";
        prompt.textContent = "DRÜCKE START UM ZU SPIELEN";
        this.demoStatus.className = "attract-demo-status";
        this.demoStatus.hidden = true;
        const demoLabel = document.createElement("strong");
        demoLabel.className = "attract-demo-label";
        demoLabel.textContent = "DEMO";
        this.demoStatus.append(demoLabel);
        this.root.dataset.presentation = "logo";
        this.root.append(logo, title, information, this.demoStatus, prompt);
        stage.append(this.root);
        this.observer = new ResizeObserver(() => this.resize());
        this.observer.observe(this.root);
        document.addEventListener("visibilitychange", this.updateAnimation);
        this.reducedMotion.addEventListener("change", this.updateAnimation);
    }

    setActive(active: boolean): void {
        if (this.destroyed || this.active === active) return;
        this.active = active;
        this.root.hidden = !active;
        if (active) {
            void this.initialize();
            this.resize();
        }
        this.updateAnimation();
    }

    setDemo(state: { mode: "laser" | "saw"; remainingMs: number; score: number; combo: number } | null): void {
        const changed = this.demo !== (state !== null);
        this.demo = state !== null;
        this.root.classList.toggle("is-demo", this.demo);
        if (this.renderer) this.renderer.domElement.hidden = this.demo;
        this.root.dataset.presentation = state?.mode ?? "logo";
        this.demoStatus.hidden = !this.demo;
        if (changed) {
            this.resize();
            this.updateAnimation();
        }
    }

    destroy(): void {
        this.destroyed = true;
        this.stop();
        this.observer.disconnect();
        document.removeEventListener("visibilitychange", this.updateAnimation);
        this.reducedMotion.removeEventListener("change", this.updateAnimation);
        this.geometry?.dispose();
        for (const material of this.materials) material.dispose();
        this.environment?.dispose();
        this.renderer?.dispose();
        this.renderer?.forceContextLoss();
        this.root.remove();
    }

    private async initialize(): Promise<void> {
        if (this.renderer || this.initializing || this.failed || this.destroyed) return;
        this.initializing = true;
        try {
            const [three, { SVGLoader }, { RoomEnvironment }, response] = await Promise.all([
                import("three"), import("three/addons/loaders/SVGLoader.js"),
                import("three/addons/environments/RoomEnvironment.js"),
                fetch("/Assets/images/ui/vollmer-emblem.svg", { signal: AbortSignal.timeout(5000) })
            ]);
            if (!response.ok) throw new Error("Missing crest");
            const source = await response.text();
            if (this.destroyed) return;
            const shapes = new SVGLoader().parse(source).paths.flatMap(path => path.toShapes());
            if (shapes.length === 0) throw new Error("Empty crest");

            this.renderer = new three.WebGLRenderer({ antialias: true, powerPreference: "low-power" });
            this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
            this.renderer.setClearColor(0xffffff, 1);
            this.renderer.toneMapping = three.ACESFilmicToneMapping;
            this.renderer.toneMappingExposure = 1.15;
            this.renderer.domElement.className = "attract-canvas";
            this.renderer.domElement.hidden = this.demo;
            this.renderer.domElement.setAttribute("aria-hidden", "true");
            this.root.prepend(this.renderer.domElement);
            this.scene = new three.Scene();
            this.camera = new three.PerspectiveCamera(32, 1, 0.1, 100);
            this.camera.position.set(0, 0, 9.5);
            const environment = new RoomEnvironment();
            const generator = new three.PMREMGenerator(this.renderer);
            try {
                this.environment = generator.fromScene(environment, 0.04);
                this.scene.environment = this.environment.texture;
            } finally {
                generator.dispose();
                environment.dispose();
            }
            this.scene.add(new three.HemisphereLight(0xffffff, 0x707781, 1.2));
            const key = new three.DirectionalLight(0xffffff, 3);
            key.position.set(-4, 5, 6);
            this.scene.add(key);
            const rim = new three.DirectionalLight(0xe4f3ff, 2);
            rim.position.set(5, 0, -3);
            this.scene.add(rim);

            const geometry = new three.ExtrudeGeometry(shapes, {
                depth: 32, steps: 1, bevelEnabled: true, bevelSize: 1.2,
                bevelThickness: 1.2, bevelSegments: 2, curveSegments: 6
            });
            this.geometry = geometry;
            geometry.scale(1, -1, 1);
            geometry.center();
            geometry.computeBoundingBox();
            const height = geometry.boundingBox!.max.y - geometry.boundingBox!.min.y;
            this.materials = [
                new three.MeshStandardMaterial({ color: 0x666d72, metalness: 0.95, roughness: 0.28 }),
                new three.MeshStandardMaterial({ color: 0x30363b, metalness: 0.95, roughness: 0.2 })
            ];
            const mesh = new three.Mesh(geometry, this.materials);
            mesh.scale.setScalar(4.6 / height);
            this.emblem = new three.Group();
            this.emblem.add(mesh);
            this.scene.add(this.emblem);
            this.root.dataset.renderer = "ready";
            this.root.dataset.contours = String(shapes.length);
            this.resize();
            this.updateAnimation();
        } catch {
            this.failed = true;
            this.root.dataset.renderer = "fallback";
            this.renderer?.domElement.remove();
            this.renderer?.dispose();
            this.renderer = undefined;
        } finally { this.initializing = false; }
    }

    private resize(): void {
        if (!this.renderer || !this.camera || !this.active || this.demo) return;
        const bounds = this.root.getBoundingClientRect();
        if (bounds.width <= 0 || bounds.height <= 0) return;
        this.renderer.setSize(bounds.width, bounds.height, false);
        this.camera.aspect = bounds.width / bounds.height;
        this.camera.updateProjectionMatrix();
        this.render(performance.now());
    }

    private readonly updateAnimation = (): void => {
        this.stop();
        if (!this.active || !this.renderer || this.demo) return;
        this.render(performance.now());
        if (!document.hidden && !this.reducedMotion.matches) this.frame = requestAnimationFrame(this.animate);
    };

    private readonly animate = (timestamp: number): void => {
        this.frame = undefined;
        if (!this.active || this.destroyed || this.demo || document.hidden || this.reducedMotion.matches) return;
        this.render(timestamp);
        this.frame = requestAnimationFrame(this.animate);
    };

    private render(timestamp: number): void {
        if (!this.renderer || !this.scene || !this.camera || !this.emblem) return;
        const seconds = timestamp / 1000;
        this.emblem.rotation.set(this.reducedMotion.matches ? -0.12 : Math.sin(seconds * 0.25) * 0.16 - 0.08,
            this.reducedMotion.matches ? 0.35 : seconds * 0.22 + 0.35, 0);
        this.renderer.render(this.scene, this.camera);
    }

    private stop(): void {
        if (this.frame !== undefined) cancelAnimationFrame(this.frame);
        this.frame = undefined;
    }
}