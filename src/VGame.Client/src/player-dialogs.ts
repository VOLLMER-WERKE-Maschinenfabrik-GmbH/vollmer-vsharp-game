export type PlayerRegistration = {
    displayName: string;
};
export type HighscoreEntry = { entryId: string; displayName: string; mode: "laser" | "saw"; score: number; completedAt: string };
export type OwnHighscore = { entry: HighscoreEntry; deletionCode: string };
type GameMode = "laser" | "saw";

export class PlayerDialogs {
    private readonly controller = new AbortController();
    private readonly registration = document.createElement("dialog");
    private readonly leaderboard = document.createElement("dialog");
    private readonly instructions = document.createElement("dialog");
    private readonly form: HTMLFormElement;
    private leaderboardSequence = 0;
    private resolveRegistration?: (value: PlayerRegistration | null | undefined) => void;
    private own?: OwnHighscore;
    private mode: GameMode = "laser";

    constructor(private readonly loadScores: (mode: GameMode) => Promise<HighscoreEntry[]>,
        private readonly deleteScore: (code: string) => Promise<boolean>) {
        this.registration.className = "cam-dialog";
        this.registration.setAttribute("aria-labelledby", "registration-title");
        this.registration.innerHTML = `
            <form class="cam-form">
                <header class="cam-header"><img src="/Assets/images/ui/vollmer-logo.png" alt="VOLLMER"><h1 id="registration-title">SPIELERDATEN</h1></header>
                <label class="cam-field" for="player-name">Anzeigename<input id="player-name" name="displayName" maxlength="40" required autocomplete="off" placeholder="Vorname oder Fantasiename"></label>
                <p class="cam-error" data-error role="alert" hidden></p>
                <footer class="cam-actions"><button type="submit">Start</button><button type="button" data-anonymous>Ohne Eintrag spielen</button><button type="button" data-cancel>Zurück</button></footer>
            </form>`;
        this.form = this.registration.querySelector("form")!;
        this.form.addEventListener("submit", event => {
            event.preventDefault();
            if (!this.form.reportValidity()) return;
            this.finishRegistration({
                displayName: this.input("displayName").value.trim()
            });
        }, { signal: this.controller.signal });
        this.registration.querySelector("[data-anonymous]")!.addEventListener("click", () => this.finishRegistration(null), { signal: this.controller.signal });
        this.registration.querySelector("[data-cancel]")!.addEventListener("click", () => this.finishRegistration(undefined), { signal: this.controller.signal });
        this.registration.addEventListener("close", () => {
            this.resolveRegistration?.(undefined);
            this.resolveRegistration = undefined;
            this.form.reset();
        }, { signal: this.controller.signal });

        this.leaderboard.className = "cam-dialog";
        this.leaderboard.setAttribute("aria-labelledby", "leaderboard-title");
        this.leaderboard.innerHTML = `
            <section class="cam-form">
                <header class="cam-header"><img src="/Assets/images/ui/vollmer-logo.png" alt="VOLLMER"><h1 id="leaderboard-title">HIGHSCORE</h1></header>
                <div class="cam-modes" aria-label="Ranglistenmodus"><button type="button" data-mode="laser" aria-pressed="true">Laser</button><button type="button" data-mode="saw" aria-pressed="false">Scheibe</button></div>
                <div class="cam-table"><table><thead><tr><th>Rang</th><th>Anzeigename</th><th>Punkte</th></tr></thead><tbody></tbody></table></div>
                <p class="cam-status" data-score-status role="status"></p>
                <div class="cam-own" hidden><p>Dein Löschcode</p><output></output><button type="button" data-delete>Meinen Eintrag löschen</button></div>
                <p class="cam-error" data-error role="alert" hidden></p>
                <footer class="cam-actions"><button type="button" data-close>Zurück</button></footer>
            </section>`;
        for (const button of this.leaderboard.querySelectorAll<HTMLButtonElement>("[data-mode]")) {
            button.addEventListener("click", () => void this.renderScores(button.dataset.mode as GameMode), { signal: this.controller.signal });
        }
        this.leaderboard.querySelector("[data-close]")!.addEventListener("click", () => this.leaderboard.close(), { signal: this.controller.signal });
        this.leaderboard.querySelector("[data-delete]")!.addEventListener("click", () => void this.deleteOwn(), { signal: this.controller.signal });
        this.leaderboard.addEventListener("close", () => {
            this.own = undefined;
            this.renderOwn();
        }, { signal: this.controller.signal });

        this.instructions.className = "cam-dialog";
        this.instructions.setAttribute("aria-labelledby", "instructions-title");
        this.instructions.innerHTML = `
            <section class="cam-form">
                <header class="cam-header"><img src="/Assets/images/ui/vollmer-logo.png" alt="VOLLMER"><h1 id="instructions-title">ANLEITUNG</h1></header>
                <div class="cam-instructions">
                    <section><h2>LASER</h2><p>Tippe auf die Schneide des Werkzeugs. Der Schaft ist tabu.</p></section>
                    <section><h2>SCHEIBE</h2><p>Beginne außerhalb der Scheibe, streiche über die Schneidkante und beende den Wisch wieder außerhalb. Triffst du den Blattkörper oder die innere Fläche, geht die Scheibe kaputt. Richtung und Länge sind egal.</p></section>
                </div>
                <footer class="cam-actions"><button type="button" data-close>Zurück</button></footer>
            </section>`;
        this.instructions.querySelector("[data-close]")!.addEventListener("click", () => this.instructions.close(), { signal: this.controller.signal });
        document.getElementById("game")!.append(this.registration, this.leaderboard, this.instructions);
    }

    requestRegistration(): Promise<PlayerRegistration | null | undefined> {
        this.close();
        this.form.reset();
        this.error(this.registration, "");
        const result = new Promise<PlayerRegistration | null | undefined>(resolve => { this.resolveRegistration = resolve; });
        this.registration.showModal();
        return result;
    }

    showLeaderboard(mode: GameMode, own?: OwnHighscore, message = ""): void {
        this.finishRegistration(undefined);
        this.own = own;
        this.renderOwn();
        this.error(this.leaderboard, "");
        if (!this.leaderboard.open) this.leaderboard.showModal();
        void this.renderScores(mode);
        if (message) this.error(this.leaderboard, message);
    }

    showInstructions(): void {
        if (this.instructions.open) return;
        this.close();
        this.instructions.showModal();
    }

    close(): void {
        this.finishRegistration(undefined);
        if (this.leaderboard.open) this.leaderboard.close();
        if (this.instructions.open) this.instructions.close();
        this.own = undefined;
        this.renderOwn();
    }

    destroy(): void {
        this.close();
        this.controller.abort();
        this.registration.remove();
        this.leaderboard.remove();
        this.instructions.remove();
    }

    private input(name: string): HTMLInputElement {
        return this.form.elements.namedItem(name) as HTMLInputElement;
    }

    private finishRegistration(value: PlayerRegistration | null | undefined): void {
        const resolve = this.resolveRegistration;
        this.resolveRegistration = undefined;
        if (this.registration.open) this.registration.close();
        this.form.reset();
        resolve?.(value);
    }

    private async renderScores(mode: GameMode): Promise<void> {
        this.mode = mode;
        const sequence = ++this.leaderboardSequence;
        for (const button of this.leaderboard.querySelectorAll<HTMLButtonElement>("[data-mode]")) button.setAttribute("aria-pressed", String(button.dataset.mode === mode));
        const status = this.leaderboard.querySelector<HTMLElement>("[data-score-status]")!;
        status.textContent = "Rangliste wird geladen …";
        try {
            const entries = await this.loadScores(mode);
            if (!this.leaderboard.open || sequence !== this.leaderboardSequence) return;
            const rows = entries.map((entry, index) => {
                const row = document.createElement("tr");
                for (const value of [String(index + 1), entry.displayName, String(entry.score)]) {
                    const cell = document.createElement("td");
                    cell.textContent = value;
                    row.append(cell);
                }
                return row;
            });
            this.leaderboard.querySelector("tbody")!.replaceChildren(...rows);
            status.textContent = entries.length === 0 ? "Noch keine Einträge." : this.own ? "Dein Ergebnis wurde gespeichert." : `${entries.length} Einträge`;
        } catch {
            if (sequence === this.leaderboardSequence) this.error(this.leaderboard, "Rangliste derzeit nicht verfügbar.");
        }
    }

    private renderOwn(): void {
        this.leaderboard.querySelector<HTMLElement>(".cam-own")!.hidden = !this.own;
        this.leaderboard.querySelector("output")!.textContent = this.own?.deletionCode ?? "";
    }

    private async deleteOwn(): Promise<void> {
        if (!this.own) return;
        const button = this.leaderboard.querySelector<HTMLButtonElement>("[data-delete]")!;
        button.disabled = true;
        try {
            if (!await this.deleteScore(this.own.deletionCode)) throw new Error();
            this.own = undefined;
            this.renderOwn();
            await this.renderScores(this.mode);
        } catch { this.error(this.leaderboard, "Der Eintrag konnte nicht gelöscht werden."); }
        finally { button.disabled = false; }
    }

    private error(dialog: HTMLDialogElement, message: string): void {
        const error = dialog.querySelector<HTMLElement>("[data-error]")!;
        error.textContent = message;
        error.hidden = !message;
    }
}