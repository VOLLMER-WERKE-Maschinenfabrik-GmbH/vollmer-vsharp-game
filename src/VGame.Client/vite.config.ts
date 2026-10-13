import { defineConfig } from "vite";

export default defineConfig({
    base: "/client/dist/",
    build: {
        outDir: "../VGame.Server/wwwroot/client/dist",
        emptyOutDir: true,
        rollupOptions: {
            input: ["play.html", "watcher.html", "zones.html"]
        }
    }
});