export type PlayerRegistration = {
    displayName: string;
    email: string | null;
    leaderboardConsent: boolean;
    prizeNotificationConsent: boolean;
    privacyVersion: string;
};
export type HighscoreEntry = { entryId: string; displayName: string; mode: "laser" | "saw"; score: number; completedAt: string };
export type OwnHighscore = { entry: HighscoreEntry; deletionCode: string };
type PlayerDataNotice = { version: string; contactEmail: string; timeZoneId: string; retention: string };
type GameMode = "laser" | "saw";

export class PlayerDialogs {
    private readonly controller = new AbortController();
    private readonly registration = document.createElement("dialog");
    private readonly leaderboard = document.createElement("dialog");
    private readonly form: HTMLFormElement;
    private notice?: PlayerDataNotice;
    private registrationSequence = 0;
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
                <label class="cam-field" for="player-name">Anzeigename<input id="player-name" name="displayName" maxlength="40" required autocomplete="off" placeholder="Name oder Fantasiename"></label>
                <label class="cam-check"><input name="leaderboardConsent" type="checkbox" required><span>Ich willige ein, dass mein Anzeigename und Ergebnis in der öffentlichen Rangliste gespeichert werden.</span></label>
                <label class="cam-check"><input name="prizeNotificationConsent" type="checkbox"><span>Ich möchte freiwillig per E-Mail über einen möglichen Gewinn benachrichtigt werden.</span></label>
                <label class="cam-field" for="player-email">E-Mail <span class="cam-optional">(freiwillig)</span><input id="player-email" name="email" type="email" maxlength="254" autocomplete="off" disabled></label>
                <details class="cam-privacy"><summary>Datenschutz</summary><p data-notice></p></details>
                <p class="cam-status" data-retention></p>
                <p class="cam-error" data-error role="alert" hidden></p>
                <footer class="cam-actions"><button type="submit" disabled>Start</button><button type="button" data-anonymous>Ohne Eintrag spielen</button><button type="button" data-cancel>Zurück</button></footer>
            </form>`;
        this.form = this.registration.querySelector("form")!;
        this.form.addEventListener("submit", event => {
            event.preventDefault();
            if (!this.notice || !this.form.reportValidity()) return;
            this.finishRegistration({
                displayName: this.input("displayName").value.trim(),
                email: this.input("prizeNotificationConsent").checked ? this.input("email").value.trim() : null,
                leaderboardConsent: this.input("leaderboardConsent").checked,
                prizeNotificationConsent: this.input("prizeNotificationConsent").checked,
                privacyVersion: this.notice.version
            });
        }, { signal: this.controller.signal });
        this.input("prizeNotificationConsent").addEventListener("change", () => this.updateEmail(), { signal: this.controller.signal });
        this.registration.querySelector("[data-anonymous]")!.addEventListener("click", () => this.finishRegistration(null), { signal: this.controller.signal });
        this.registration.querySelector("[data-cancel]")!.addEventListener("click", () => this.finishRegistration(undefined), { signal: this.controller.signal });
        this.registration.addEventListener("close", () => {
            this.resolveRegistration?.(undefined);
            this.resolveRegistration = undefined;
            this.form.reset();
            this.updateEmail();
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
        document.getElementById("game")!.append(this.registration, this.leaderboard);
    }

    requestRegistration(): Promise<PlayerRegistration | null | undefined> {
        this.close();
        this.notice = undefined;
        this.form.reset();
        this.updateEmail();
        this.form.querySelector<HTMLButtonElement>("[type=submit]")!.disabled = true;
        this.form.querySelector<HTMLElement>("[data-notice]")!.textContent = "";
        this.form.querySelector<HTMLElement>("[data-retention]")!.textContent = "Datenschutzhinweis wird geladen …";
        this.error(this.registration, "");
        const result = new Promise<PlayerRegistration | null | undefined>(resolve => { this.resolveRegistration = resolve; });
        this.registration.showModal();
        void this.loadNotice(++this.registrationSequence);
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

    close(): void {
        this.finishRegistration(undefined);
        if (this.leaderboard.open) this.leaderboard.close();
        this.own = undefined;
        this.renderOwn();
    }

    destroy(): void {
        this.close();
        this.controller.abort();
        this.registration.remove();
        this.leaderboard.remove();
    }

    private input(name: string): HTMLInputElement {
        return this.form.elements.namedItem(name) as HTMLInputElement;
    }

    private updateEmail(): void {
        const email = this.input("email");
        email.disabled = !this.input("prizeNotificationConsent").checked;
        email.required = !email.disabled;
        if (email.disabled) email.value = "";
    }

    private finishRegistration(value: PlayerRegistration | null | undefined): void {
        const resolve = this.resolveRegistration;
        this.resolveRegistration = undefined;
        if (this.registration.open) this.registration.close();
        this.form.reset();
        this.updateEmail();
        resolve?.(value);
    }

    private async loadNotice(sequence: number): Promise<void> {
        try {
            const response = await fetch("/api/player-data", { cache: "no-store", signal: AbortSignal.timeout(5000) });
            if (!response.ok) throw new Error();
            const notice = await response.json() as PlayerDataNotice;
            if (![notice.version, notice.contactEmail, notice.retention, notice.timeZoneId].every(value => typeof value === "string" && value.length > 0)) throw new Error();
            if (!this.registration.open || sequence !== this.registrationSequence) return;
            this.notice = notice;
            this.form.querySelector<HTMLElement>("[data-notice]")!.textContent =
                `Verantwortlich: VOLLMER WERKE Maschinenfabrik GmbH, Biberach/Riß. Kontakt: ${notice.contactEmail}. ` +
                "Anzeigename, Spielmodus, Ergebnis, Zeitpunkt und Einwilligungsnachweis werden mit deiner freiwilligen Einwilligung nach Art. 6 Abs. 1 lit. a DSGVO lokal gespeichert. " +
                "Nur Anzeigename, Rang und Punkte erscheinen öffentlich. Eine freiwillig angegebene E-Mail wird ausschließlich zur Gewinnbenachrichtigung verwendet, nicht für Werbung oder auf Zuschaueranzeigen. " +
                "Es erfolgt keine Cloudübertragung. Du kannst deine Einwilligung jederzeit widerrufen und Auskunft, Berichtigung oder Löschung verlangen. Dein Löschcode wird nur dir nach der Runde angezeigt. " +
                "Die Verarbeitung bis zum Widerruf bleibt rechtmäßig. Du kannst dich bei der zuständigen Datenschutzaufsichtsbehörde beschweren.";
            this.form.querySelector<HTMLElement>("[data-retention]")!.textContent = notice.retention;
            this.form.querySelector<HTMLButtonElement>("[type=submit]")!.disabled = false;
        } catch {
            if (sequence === this.registrationSequence && this.registration.open) {
                this.error(this.registration, "Datenschutzhinweis nicht verfügbar. Spielen ohne Eintrag bleibt möglich.");
            }
        }
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