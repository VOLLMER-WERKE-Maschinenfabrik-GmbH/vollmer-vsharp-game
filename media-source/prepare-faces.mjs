import assert from "node:assert/strict";
import { mkdir, readdir, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const require = createRequire(path.join(root, "src/VGame.Client/package.json"));
const sharp = require("sharp");
sharp.cache(false);
const destination = path.join(root, "Assets/images/faces");
const catalog = { version: 1, states: { dull: [], inProgress: [], sharp: [], failed: [] } };
const groups = [
    ["stumpf_gelangweilt", "dull", "bored"], ["stumpf_nervoes", "dull", "nervous"], ["stump_frech", "dull", "cheeky"],
    ["schleifen", "inProgress", "grinding"], ["scharf_begeistert", "sharp", "excited"], ["scharf_stolz", "sharp", "proud"],
    ["defekt_benommen", "failed", "dazed"], ["defekt_erschrocken", "failed", "startled"]
];
await mkdir(destination, { recursive: true });
for (const [folder, state, expression] of groups) {
    const source = path.join(root, "media-source/leonardo/faces", folder);
    const files = (await readdir(source)).filter(filename => /\.(png|webp)$/i.test(filename)).sort((first, second) => first.localeCompare(second, "en", { numeric: true }));
    assert.ok(files.length > 0, `${folder}: no face images`);
    for (const [index, filename] of files.entries()) {
        const image = sharp(path.join(source, filename));
        const metadata = await image.metadata();
        assert.ok(metadata.hasAlpha, `${folder}/${filename}: missing alpha channel`);
        const { data, info } = await image.ensureAlpha().raw().toBuffer({ resolveWithObject: true });
        let left = info.width;
        let top = info.height;
        let right = -1;
        let bottom = -1;
        let transparent = false;
        for (let vertical = 0; vertical < info.height; vertical++) {
            for (let horizontal = 0; horizontal < info.width; horizontal++) {
                const alpha = data[(vertical * info.width + horizontal) * 4 + 3];
                if (alpha === 0) transparent = true;
                if (alpha < 8) continue;
                left = Math.min(left, horizontal);
                right = Math.max(right, horizontal);
                top = Math.min(top, vertical);
                bottom = Math.max(bottom, vertical);
            }
        }
        assert.ok(transparent && right >= left && bottom >= top, `${folder}/${filename}: empty or non-transparent image`);
        const output = `face_${state}_${expression}_${String(index + 1).padStart(2, "0")}.webp`;
        await sharp(path.join(source, filename)).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
            .resize(512, 512, { fit: "inside", withoutEnlargement: true })
            .extend({ top: 12, bottom: 12, left: 12, right: 12, background: { r: 0, g: 0, b: 0, alpha: 0 } })
            .webp({ lossless: true }).toFile(path.join(destination, output));
        const prepared = await sharp(path.join(destination, output)).metadata();
        assert.ok(prepared.hasAlpha && prepared.width <= 536 && prepared.height <= 536, output);
        catalog.states[state].push(output);
    }
    console.log(`${folder}: ${files.length} transparent ${state} variants prepared`);
}
await writeFile(path.join(destination, "catalog.json"), JSON.stringify(catalog, null, 2) + "\n");
console.log(`Prepared ${Object.values(catalog.states).flat().length} runtime faces; source images unchanged.`);