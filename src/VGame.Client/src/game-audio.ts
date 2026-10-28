type AudioPool = { poolId: string; variants: string[]; volumeProfile: string; fallback: string; bagRule: string };
type SoundChannel = { source: AudioBufferSourceNode | OscillatorNode; gain: GainNode; voice: boolean };
type MusicTrack = { file: string; startSeconds: number; volume: number };

export class ShuffleBag {
    private pending: string[] = [];
    private previous: string | undefined;
    constructor(private readonly variants: string[]) {}
    next(): string | undefined {
        if (this.variants.length === 0) return undefined;
        if (this.pending.length === 0) {
            this.pending = [...this.variants];
            for (let index = this.pending.length - 1; index > 0; index--) {
                const other = Math.floor(Math.random() * (index + 1));
                [this.pending[index], this.pending[other]] = [this.pending[other], this.pending[index]];
            }
            if (this.pending.length > 1 && this.pending.at(-1) === this.previous) {
                [this.pending[0], this.pending[this.pending.length - 1]] = [this.pending[this.pending.length - 1], this.pending[0]];
            }
        }
        this.previous = this.pending.pop();
        return this.previous;
    }
}

export class GameAudio {
    private context?: AudioContext;
    private master?: GainNode;
    private active = false;
    private destroyed = false;
    private playing = false;
    private volume = 1;
    private roundId: string | null = null;
    private readonly pools = new Map<string, AudioPool>();
    private readonly bags = new Map<string, ShuffleBag>();
    private readonly encoded = new Map<string, ArrayBuffer>();
    private readonly buffers = new Map<string, AudioBuffer>();
    private readonly channels = new Set<SoundChannel>();
    private readonly events = new Set<string>();
    private voice?: SoundChannel;
    private effect?: SoundChannel;
    private lastVoiceStart = Number.NEGATIVE_INFINITY;
    private pendingVoice?: { pool: string; createdAt: number; roundId: string };
    private voiceTimer?: ReturnType<typeof setTimeout>;
    private decodePromise?: Promise<void>;
    private musicTrack?: MusicTrack;
    private menuMusicTrack?: MusicTrack;
    private currentMusicTrack?: MusicTrack;
    private musicSource?: AudioBufferSourceNode;
    private musicGain?: GainNode;
    private duckTimer?: ReturnType<typeof setTimeout>;
    private readonly preloadPromise: Promise<void>;

    constructor(private readonly report: (message: string) => void) {
        this.preloadPromise = this.preload();
    }

    private async preload(): Promise<void> {
        try {
            const response = await fetch("/api/game-audio", { cache: "no-store", signal: AbortSignal.timeout(5000) });
            if (!response.ok) throw new Error();
            const catalog = await response.json() as { pools: AudioPool[]; music: MusicTrack | null; menuMusic?: MusicTrack | null };
            this.musicTrack = catalog.music ?? undefined;
            this.menuMusicTrack = catalog.menuMusic ?? undefined;
            for (const pool of catalog.pools) {
                this.pools.set(pool.poolId, pool);
                this.bags.set(pool.poolId, new ShuffleBag(pool.variants));
            }
            const files = [...new Set(catalog.pools.flatMap(pool => pool.variants))];
            if (this.musicTrack) files.push(this.musicTrack.file);
            if (this.menuMusicTrack) files.push(this.menuMusicTrack.file);
            await Promise.all(files.map(async filename => {
                try {
                    const audio = await fetch(`/Assets/audio/${filename}`, { signal: AbortSignal.timeout(5000) });
                    if (!audio.ok) throw new Error();
                    this.encoded.set(filename, await audio.arrayBuffer());
                } catch { this.report("Ein Audioclip fehlt. Ersatzfeedback bleibt aktiv."); }
            }));
        } catch { this.report("Audiopools nicht verfügbar. Ersatzfeedback bleibt aktiv."); }
    }

    async unlock(): Promise<void> {
        if (!this.active || this.destroyed) return;
        try {
            if (!this.context) {
                this.context = new AudioContext();
                this.master = this.context.createGain();
                const limiter = this.context.createDynamicsCompressor();
                limiter.threshold.value = -3;
                limiter.knee.value = 0;
                limiter.ratio.value = 20;
                limiter.attack.value = 0.003;
                limiter.release.value = 0.12;
                this.master.connect(limiter).connect(this.context.destination);
                this.context.addEventListener("statechange", this.onContextStateChange);
            }
            this.applyVolume();
            if (!this.decodePromise) this.decodePromise = this.decode();
            await Promise.all([this.context.resume(), this.decodePromise]);
            this.syncMusic();
            if (this.context.state !== "running") this.report("Audio ist durch den Browser gesperrt. Erneut Start drücken.");
        } catch { if (!this.destroyed) this.report("Audio kann nicht gestartet werden. Das Spiel bleibt spielbar."); }
    }

    private readonly onContextStateChange = (): void => {
        if (this.destroyed) return;
        this.applyVolume();
        this.syncMusic();
    };

    private async decode(): Promise<void> {
        await this.preloadPromise;
        await Promise.all([...this.encoded].map(async ([filename, bytes]) => {
            try { this.buffers.set(filename, await this.context!.decodeAudioData(bytes.slice(0))); }
            catch { this.report("Ein Audioclip kann nicht dekodiert werden. Ersatzfeedback bleibt aktiv."); }
        }));
    }

    setVolume(volume: number): void {
        this.volume = Math.max(0, Math.min(1, volume));
        this.applyVolume();
        if (this.volume === 0) this.stopEffects();
        this.syncMusic();
    }

    setRound(roundId: string | null, active: boolean, playing: boolean): void {
        const becameActive = active && !this.active;
        if (this.roundId !== roundId) { this.events.clear(); this.lastVoiceStart = Number.NEGATIVE_INFINITY; this.stopAll(); }
        this.roundId = roundId;
        this.active = active;
        this.playing = playing;
        if (!active || !playing) this.stopEffects();
        this.applyVolume();
        this.syncMusic();
        if (becameActive) void this.unlock();
    }

    play(eventId: string, roundId: string, voicePool: string | null, effectPool: string): void {
        if (!this.active || !this.playing || roundId !== this.roundId || this.events.has(eventId)) return;
        this.events.add(eventId);
        if (this.events.size > 1024) this.events.delete(this.events.values().next().value!);
        if (this.volume === 0 || this.context?.state !== "running") return;
        this.startPool(effectPool, false, true);
        if (voicePool === null) {
            clearTimeout(this.voiceTimer);
            this.pendingVoice = undefined;
            return;
        }
        const now = performance.now();
        const wait = 180 - (now - this.lastVoiceStart);
        clearTimeout(this.voiceTimer);
        if (wait <= 0) { this.pendingVoice = undefined; this.startPool(voicePool, true, false); }
        else {
            this.pendingVoice = { pool: voicePool, createdAt: now, roundId };
            this.voiceTimer = setTimeout(() => {
                const pending = this.pendingVoice;
                this.pendingVoice = undefined;
                if (pending && this.active && this.playing && pending.roundId === this.roundId && performance.now() - pending.createdAt <= 200 && this.volume > 0)
                    this.startPool(pending.pool, true, false);
            }, wait);
        }
    }

    private startPool(poolId: string, voice: boolean, replacementTone: boolean): void {
        if (!this.context || !this.master || !this.active || this.volume === 0) return;
        let current = poolId;
        const visited = new Set<string>();
        let buffer: AudioBuffer | undefined;
        while (current !== "generated_tone" && !visited.has(current)) {
            visited.add(current);
            const pool = this.pools.get(current);
            const available = pool?.variants.some(filename => this.buffers.has(filename));
            if (available) {
                for (let attempt = 0; attempt < pool!.variants.length; attempt++) {
                    const filename = this.bags.get(current)?.next();
                    if (filename && this.buffers.has(filename)) { buffer = this.buffers.get(filename); break; }
                }
                if (buffer) break;
            }
            if (voice) return;
            current = pool?.fallback ?? "generated_tone";
        }
        if (!buffer && !replacementTone) return;
        this.stopChannel(voice ? this.voice : this.effect, voice ? 0.015 : 0.003);
        const gain = this.context.createGain();
        gain.gain.value = voice ? 0.5 : 0.2;
        gain.connect(this.master);
        let source: AudioBufferSourceNode | OscillatorNode;
        if (buffer) {
            const player = this.context.createBufferSource();
            player.buffer = buffer;
            source = player;
        } else {
            const oscillator = this.context.createOscillator();
            oscillator.type = "sine";
            oscillator.frequency.value = 680;
            source = oscillator;
            gain.gain.setValueAtTime(0.2, this.context.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, this.context.currentTime + 0.07);
        }
        source.connect(gain);
        const channel: SoundChannel = { source, gain, voice };
        this.channels.add(channel);
        if (voice) { this.voice = channel; this.lastVoiceStart = performance.now(); }
        else this.effect = channel;
        source.onended = () => {
            this.channels.delete(channel);
            source.disconnect(); gain.disconnect();
            if (this.voice === channel) this.voice = undefined;
            if (this.effect === channel) this.effect = undefined;
            this.restoreMusic();
        };
        const delay = voice ? 0.015 : 0.003;
        const start = this.context.currentTime + delay;
        source.start(start);
        if (buffer) source.stop(start + buffer.duration);
        else source.stop(start + 0.075);
        this.duckMusic();
    }

    private stopChannel(channel: SoundChannel | undefined, fade: number): void {
        if (!channel || !this.context) return;
        try {
            const now = this.context.currentTime;
            channel.gain.gain.cancelScheduledValues(now);
            channel.gain.gain.setValueAtTime(channel.gain.gain.value, now);
            channel.gain.gain.linearRampToValueAtTime(0, now + fade);
            channel.source.stop(now + fade);
        } catch {}
    }

    private applyVolume(): void {
        if (this.context && this.master) this.master.gain.setValueAtTime(this.active ? this.volume : 0, this.context.currentTime);
    }

    private stopEffects(): void {
        clearTimeout(this.voiceTimer);
        this.pendingVoice = undefined;
        for (const channel of this.channels) this.stopChannel(channel, 0);
        this.voice = this.effect = undefined;
    }

    private syncMusic(): void {
        const track = this.playing && this.roundId ? this.musicTrack : this.menuMusicTrack;
        if (!this.active || this.currentMusicTrack !== track) this.stopMusic();
        if (!this.active || this.musicSource || this.context?.state !== "running" || !this.master || !track) return;
        const buffer = this.buffers.get(track.file);
        if (!buffer) return;
        const start = track.startSeconds;
        if (buffer.duration <= start) { this.report("Hintergrundmusik ist kürzer als der konfigurierte Startpunkt."); return; }
        const source = this.context.createBufferSource();
        source.buffer = buffer;
        source.loop = true;
        source.loopStart = start;
        source.loopEnd = buffer.duration;
        const gain = this.context.createGain();
        gain.gain.value = track.volume;
        source.connect(gain).connect(this.master);
        this.musicSource = source;
        this.musicGain = gain;
        this.currentMusicTrack = track;
        source.onended = () => { source.disconnect(); gain.disconnect(); };
        source.start(this.context.currentTime, start);
    }

    private duckMusic(): void {
        clearTimeout(this.duckTimer);
        if (!this.context || !this.musicGain || !this.currentMusicTrack) return;
        const now = this.context.currentTime;
        this.musicGain.gain.cancelScheduledValues(now);
        this.musicGain.gain.setValueAtTime(this.musicGain.gain.value, now);
        this.musicGain.gain.linearRampToValueAtTime(this.currentMusicTrack.volume * Math.pow(10, -6 / 20), now + 0.08);
    }

    private restoreMusic(): void {
        if (this.effect || this.voice || !this.musicSource) return;
        clearTimeout(this.duckTimer);
        this.duckTimer = setTimeout(() => {
            if (this.effect || this.voice || !this.context || !this.musicGain || !this.currentMusicTrack) return;
            const now = this.context.currentTime;
            this.musicGain.gain.cancelScheduledValues(now);
            this.musicGain.gain.setValueAtTime(this.musicGain.gain.value, now);
            this.musicGain.gain.linearRampToValueAtTime(this.currentMusicTrack.volume, now + 0.25);
        }, 120);
    }

    private stopMusic(): void {
        clearTimeout(this.duckTimer);
        if (this.musicSource) {
            try { this.musicSource.stop(); } catch {}
        }
        this.musicSource = undefined;
        this.musicGain = undefined;
        this.currentMusicTrack = undefined;
    }

    stopAll(): void { this.stopEffects(); this.stopMusic(); }

    destroy(): void {
        this.destroyed = true;
        this.stopAll();
        this.context?.removeEventListener("statechange", this.onContextStateChange);
        void this.context?.close();
    }
}