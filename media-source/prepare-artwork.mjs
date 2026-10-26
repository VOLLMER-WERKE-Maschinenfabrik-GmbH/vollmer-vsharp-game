import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import path from "node:path";

const repositoryRoot = fileURLToPath(new URL("../", import.meta.url));
const require = createRequire(path.join(repositoryRoot, "src/VGame.Client/package.json"));
const sharp = require("sharp");
const sourceDirectory = path.join(repositoryRoot, "media-source/leonardo");
const masterDirectory = path.join(repositoryRoot, "media-source/affinity");
const imageDirectory = path.join(repositoryRoot, "Assets/images");
await mkdir(masterDirectory, { recursive: true });
await mkdir(path.join(imageDirectory, "tools"), { recursive: true });
await mkdir(path.join(imageDirectory, "background"), { recursive: true });

if (!process.argv.includes("--tools-only")) {
    const stage = sharp(path.join(sourceDirectory, "bg_0001_v08.jpg"))
        .resize(2560, 1440, { fit: "cover", position: "centre" });
    await stage.clone().png().toFile(path.join(masterDirectory, "bg_0001.png"));
    await stage.webp({ quality: 92 }).toFile(path.join(imageDirectory, "background/bg_0001.webp"));
    const stageMetadata = await sharp(path.join(imageDirectory, "background/bg_0001.webp")).metadata();
    assert.equal(stageMetadata.width, 2560);
    assert.equal(stageMetadata.height, 1440);
}

const catalog = [];
for (const asset of [
    { assetId: "tool_0002", variant: "v01", visualKind: "drill", cutStart: 0.88 },
    { assetId: "tool_0006", variant: "v03", visualKind: "cutter", cutStart: 0.67 },
    { assetId: "tool_0010", variant: "v01", visualKind: "saw" }
]) {
    const { data, info } = await sharp(path.join(sourceDirectory, `${asset.assetId}_${asset.variant}.jpg`))
        .removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const rgba = Buffer.alloc(info.width * info.height * 4);
    let minimumX = info.width;
    let minimumY = info.height;
    let maximumX = 0;
    let maximumY = 0;
    for (let pixelY = 0; pixelY < info.height; pixelY++) {
        for (let pixelX = 0; pixelX < info.width; pixelX++) {
            const sourceOffset = (pixelY * info.width + pixelX) * 3;
            const targetOffset = (pixelY * info.width + pixelX) * 4;
            const red = data[sourceOffset];
            const green = data[sourceOffset + 1];
            const blue = data[sourceOffset + 2];
            const isBackground = red > green + 18 && red > blue + 12;
            rgba[targetOffset] = red;
            rgba[targetOffset + 1] = green;
            rgba[targetOffset + 2] = blue;
            rgba[targetOffset + 3] = isBackground ? 0 : 255;
            if (!isBackground) {
                minimumX = Math.min(minimumX, pixelX);
                minimumY = Math.min(minimumY, pixelY);
                maximumX = Math.max(maximumX, pixelX);
                maximumY = Math.max(maximumY, pixelY);
            }
        }
    }
    assert.ok(maximumX > minimumX && maximumY > minimumY, `${asset.assetId}: empty silhouette`);
    let prepared = sharp(rgba, { raw: { width: info.width, height: info.height, channels: 4 } })
        .extract({ left: minimumX, top: minimumY, width: maximumX - minimumX + 1, height: maximumY - minimumY + 1 });
    if (asset.visualKind !== "saw") {
        prepared = sharp(await prepared.rotate(90).png().toBuffer());
    }
    const resized = await prepared.resize(896, 896, { fit: "inside" }).png().toBuffer();
    const resizedMetadata = await sharp(resized).metadata();
    const padding = 64;
    const master = await sharp(resized).extend({ top: padding, bottom: padding, left: padding, right: padding, background: "#00000000" })
        .png().toBuffer();
    const width = resizedMetadata.width + padding * 2;
    const height = resizedMetadata.height + padding * 2;
    const pixels = await sharp(master).raw().toBuffer();
    const alphaAt = (pixelX, pixelY) => pixels[(pixelY * width + pixelX) * 4 + 3];
    const profile = [];
    for (let pixelX = padding; pixelX < width - padding; pixelX++) {
        let top = height;
        let bottom = -1;
        for (let pixelY = padding; pixelY < height - padding; pixelY++) {
            if (alphaAt(pixelX, pixelY) >= 128) {
                top = Math.min(top, pixelY);
                bottom = Math.max(bottom, pixelY + 1);
            }
        }
        if (bottom >= 0) {
            profile.push({ pixelX, top, bottom });
        }
    }
    const outline = columns => {
        const bands = [];
        for (let index = 0; index < columns.length; index += 6) {
            const block = columns.slice(index, index + 6);
            bands.push({ start: block[0].pixelX, end: block.at(-1).pixelX + 1,
                top: Math.min(...block.map(column => column.top)), bottom: Math.max(...block.map(column => column.bottom)) });
        }
        return [
            ...bands.flatMap(band => [{ x: band.start / width, y: band.top / height }, { x: band.end / width, y: band.top / height }]),
            ...bands.toReversed().flatMap(band => [{ x: band.end / width, y: band.bottom / height }, { x: band.start / width, y: band.bottom / height }])
        ];
    };
    let hitZones;
    if (asset.visualKind === "saw") {
        const bodyRadius = 0.375 * Math.min(resizedMetadata.width / width, resizedMetadata.height / height);
        const body = Array.from({ length: 64 }, (_, index) => {
            const angle = index * Math.PI / 32;
            return { x: 0.5 + Math.cos(angle) * bodyRadius, y: 0.5 + Math.sin(angle) * bodyRadius };
        });
        hitZones = [
            { zoneId: "shaft", role: "shaft", points: body },
            { zoneId: "cut", role: "cut", points: outline(profile) }
        ];
    } else {
        const split = padding + Math.round(resizedMetadata.width * asset.cutStart);
        hitZones = [
            { zoneId: "shaft", role: "shaft", points: outline(profile.filter(column => column.pixelX < split)) },
            { zoneId: "cut", role: "cut", points: outline(profile.filter(column => column.pixelX >= split)) }
        ];
    }
    for (const zone of hitZones) {
        assert.ok(zone.points.length >= 3, `${asset.assetId}: invalid ${zone.role} polygon`);
        assert.ok(zone.points.every(point => point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1));
    }
    assert.equal(alphaAt(0, 0), 0);
    if (asset.visualKind === "saw") {
        assert.equal(alphaAt(Math.floor(width / 2), Math.floor(height / 2)), 0, "Saw mounting hole must be transparent");
    }
    await writeFile(path.join(masterDirectory, `${asset.assetId}.png`), master);
    await sharp(master).webp({ lossless: true }).toFile(path.join(imageDirectory, `tools/${asset.assetId}.webp`));
    catalog.push({
        toolId: asset.assetId,
        assetId: asset.assetId,
        zOrder: 0,
        visualKind: asset.visualKind,
        hasPkd: false,
        cutDirectionDegrees: 0,
        boundingWidthNormalized: asset.visualKind === "saw" ? 0.12 : 0.20,
        boundingHeightNormalized: (asset.visualKind === "saw" ? 0.12 : 0.20) * 16 / 9 * height / width,
        hitZones
    });
    console.log(`${asset.assetId}: ${width}x${height}, transparent master and lossless WebP prepared`);
}
await writeFile(path.join(repositoryRoot, "Assets/config/artwork.prototype.json"), `${JSON.stringify(catalog, null, 2)}\n`);
console.log("Artwork prepared; prototype zones still require visual review, not a production manifest.");