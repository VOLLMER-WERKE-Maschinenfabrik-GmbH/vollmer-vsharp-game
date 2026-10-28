import assert from "node:assert/strict";
import { mkdir } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const require = createRequire(path.join(root, "src/VGame.Client/package.json"));
const sharp = require("sharp");
sharp.cache(false);
const destination = path.join(root, "Assets/images/sparks");
await mkdir(destination, { recursive: true });

const ramp = (value, lower, upper) => {
    const fraction = Math.max(0, Math.min(1, (value - lower) / (upper - lower)));
    return fraction * fraction * (3 - 2 * fraction);
};

for (const group of ["grind", "laser", "mistake", "single"]) {
    for (let variant = 1; variant <= 4; variant++) {
        const name = `${group}_spark${variant}`;
        const source = path.join(root, "media-source/leonardo/sparks", `${name}.png`);
        const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
        const blurred = await sharp(source).flatten({ background: "#000000" }).blur(12).ensureAlpha().raw().toBuffer();
        let center = { x: 0, y: 0 };
        let brightest = -1;
        let left = info.width;
        let right = -1;
        let top = info.height;
        let bottom = -1;
        for (let vertical = 0; vertical < info.height; vertical++) {
            for (let horizontal = 0; horizontal < info.width; horizontal++) {
                const offset = (vertical * info.width + horizontal) * 4;
                const coverage = data[offset + 3] / 255;
                const maximum = Math.max(data[offset], data[offset + 1], data[offset + 2]) * coverage;
                const minimum = Math.min(data[offset], data[offset + 1], data[offset + 2]) * coverage;
                const blurredMaximum = Math.max(blurred[offset], blurred[offset + 1], blurred[offset + 2]);
                // Flächiger Nebel wird unterdrückt; helle Kerne und scharfe, farbige Funken bleiben erhalten.
                const foreground = Math.max(ramp(minimum, 205, 250),
                    ramp(maximum - minimum, 125, 210),
                    ramp(maximum - blurredMaximum, 10, 65) * ramp(maximum, 65, 160));
                data[offset + 3] = Math.round(data[offset + 3] * foreground);
                if (data[offset + 3] < 4) {
                    data[offset + 3] = 0;
                    continue;
                }
                left = Math.min(left, horizontal);
                right = Math.max(right, horizontal);
                top = Math.min(top, vertical);
                bottom = Math.max(bottom, vertical);
                const intensity = Math.min(blurred[offset], blurred[offset + 1], blurred[offset + 2]);
                if (intensity > brightest) {
                    brightest = intensity;
                    center = { x: horizontal, y: vertical };
                }
            }
        }
        assert.ok(right > left && bottom > top, `${name}: empty effect`);
        const padding = 12;
        let image = sharp(data, { raw: info }).extract({ left, top, width: right - left + 1, height: bottom - top + 1 });
        if (group === "single") {
            image = image.resize(256, 64, { fit: "inside", withoutEnlargement: true })
                .extend({ top: 4, bottom: 4, left: 4, right: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } });
        } else {
            // Der Kontaktkern wird zentriert, damit das Sprite exakt am bestätigten Trefferpunkt sitzt.
            const radius = Math.max(center.x - left, right - center.x, center.y - top, bottom - center.y) + padding;
            const centered = await image.extend({
                left: radius - (center.x - left),
                right: radius - (right - center.x),
                top: radius - (center.y - top),
                bottom: radius - (bottom - center.y),
                background: { r: 0, g: 0, b: 0, alpha: 0 }
            }).png().toBuffer();
            image = sharp(centered).resize(512, 512);
        }
        const output = path.join(destination, `${name}.webp`);
        await image.webp({ lossless: true }).toFile(output);
        const prepared = await sharp(output).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
        assert.ok(prepared.info.width <= 512 && prepared.info.height <= 512, `${name}: oversized texture`);
        let visible = 0;
        for (let vertical = 0; vertical < prepared.info.height; vertical++) {
            for (let horizontal = 0; horizontal < prepared.info.width; horizontal++) {
                const alpha = prepared.data[(vertical * prepared.info.width + horizontal) * 4 + 3];
                if (alpha > 0) visible++;
                if (horizontal === 0 || vertical === 0 || horizontal === prepared.info.width - 1 || vertical === prepared.info.height - 1) {
                    assert.equal(alpha, 0, `${name}: non-transparent border`);
                }
            }
        }
        const maximumCoverage = group === "single" ? 0.5 : 0.2;
        assert.ok(visible > 0 && visible < prepared.info.width * prepared.info.height * maximumCoverage,
            `${name}: empty or diffuse effect`);
        if (group !== "single") {
            const centerOffset = (256 * prepared.info.width + 256) * 4 + 3;
            assert.ok(prepared.data[centerOffset] > 128, `${name}: contact core is not centered`);
        }
        console.log(`${name}: ${prepared.info.width}x${prepared.info.height}, transparent border verified`);
    }
}
console.log("Prepared 16 runtime spark textures; source images unchanged.");
