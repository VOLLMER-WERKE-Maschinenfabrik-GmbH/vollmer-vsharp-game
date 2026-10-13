using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VGame.Contracts;
using VGame.Domain;

namespace VGame.Tools.Manifest;

public sealed record ManifestIssue(string AssetId, string Message, int ExitCode, bool Planned = false);
public sealed record ManifestBuildResult(int ExitCode, IReadOnlyList<ManifestIssue> Issues, bool Written);

public sealed class ManifestBuilder(IReadOnlyList<AudioPoolManifestEntry>? audioPools = null)
{
    public ManifestBuildResult Run(ManifestPaths paths, bool write)
    {
        var issues = new List<ManifestIssue>();
        try
        {
            paths.Validate();
            var registerPath = Path.Combine(paths.AssetsDirectory, "config/id-register.json");
            var register = ManifestInputs.ReadRegister(registerPath);
            var validator = new AssetValidator(Path.Combine(paths.AssetsDirectory, "templates"));
            var configurationPath = Path.Combine(paths.AssetsDirectory, "config/gameplay.json");
            var gameplay = JsonSerializer.Deserialize<GameplayOptions>(File.ReadAllText(configurationPath), ManifestInputs.JsonOptions)
                ?? throw new ManifestValidationException("Gameplay configuration is empty.");
            gameplay.Validate();
            var metadataDirectory = Path.Combine(paths.SourceDirectory, "assets");
            if (!Directory.Exists(metadataDirectory)) throw new DirectoryNotFoundException("Authoring metadata directory is missing.");
            var metadata = new SortedDictionary<string, AssetMetadata>(StringComparer.Ordinal);
            var metadataPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var filename in Directory.GetFiles(metadataDirectory, "*.meta.json").Order(StringComparer.Ordinal))
            {
                var entry = validator.ReadMetadata(filename);
                ManifestInputs.ValidateAssignment(entry, register);
                if (!metadata.TryAdd(entry.AssetId, entry)) throw new ManifestValidationException($"{entry.AssetId}: duplicate metadata.");
                metadataPaths.Add(entry.AssetId, filename);
            }
            ValidateRegisteredFiles(paths.AssetsDirectory, register);
            var assets = new List<JsonObject>();
            var planned = new List<JsonObject>();
            var tools = new List<ToolManifestEntry>();
            var stages = new List<StageManifestEntry>();
            var pools = (audioPools ?? AudioPoolBuilder.Read(paths.AssetsDirectory)).OrderBy(pool => pool.PoolId, StringComparer.Ordinal).ToArray();
            ValidateAudioPools(paths.AssetsDirectory, pools);
            foreach (var reservation in register.Values.OrderBy(entry => entry.AssetId, StringComparer.Ordinal))
            {
                var template = validator.GetTemplate(reservation.Category);
                if (!Regex.IsMatch(reservation.AssetId, template["properties"]!["assetId"]!["pattern"]!.GetValue<string>(), RegexOptions.CultureInvariant))
                    throw new ManifestValidationException($"{reservation.AssetId}: ID prefix does not match category.");
                if (!metadata.TryGetValue(reservation.AssetId, out var entry))
                {
                    if (reservation.Status == "assigned") throw new FileNotFoundException($"{reservation.AssetId}: assigned metadata is missing.");
                    planned.Add(new() { ["assetId"] = reservation.AssetId, ["category"] = reservation.Category, ["status"] = "planned" });
                    issues.Add(new(reservation.AssetId, "Planned asset has no complete assigned metadata.", 3, true));
                    continue;
                }
                var filename = FindImage(paths.AssetsDirectory, entry, template);
                using var bitmap = AssetValidator.ValidateImage(filename, template);
                var relativeFile = Path.GetRelativePath(paths.AssetsDirectory, filename).Replace('\\', '/');
                var asset = new JsonObject
                {
                    ["assetId"] = entry.AssetId, ["category"] = entry.Category, ["file"] = relativeFile,
                    ["width"] = bitmap.Width, ["height"] = bitmap.Height, ["format"] = Path.GetExtension(filename).TrimStart('.'),
                    ["byteLength"] = new FileInfo(filename).Length, ["checksum"] = Hash(File.ReadAllBytes(filename)),
                    ["metadataChecksum"] = Hash(File.ReadAllBytes(metadataPaths[entry.AssetId])),
                    ["licenseStatus"] = entry.LicenseStatus, ["source"] = entry.Source
                };
                if (template["x-requiredCompanionFormat"] is not null)
                {
                    var companion = Path.ChangeExtension(filename, template["x-requiredCompanionFormat"]!.GetValue<string>());
                    using var atlas = JsonDocument.Parse(File.ReadAllText(companion));
                    if (atlas.RootElement.ValueKind != JsonValueKind.Object) throw new ManifestValidationException($"{entry.AssetId}: atlas JSON must be an object.");
                    asset["atlasFile"] = Path.GetRelativePath(paths.AssetsDirectory, companion).Replace('\\', '/');
                    asset["atlasChecksum"] = Hash(File.ReadAllBytes(companion));
                }
                if (entry.Category == "tool-image")
                {
                    var zonePath = Path.Combine(paths.SourceDirectory, "zone-authoring", entry.AssetId + ".zones.json");
                    var zones = AssetValidator.ValidateZones(zonePath, entry, bitmap);
                    asset["zoneChecksum"] = Hash(File.ReadAllBytes(zonePath));
                    tools.Add(CreateTool(entry, zones, relativeFile, pools, metadata));
                }
                else if (entry.Category is "showroom-stage" or "showroom-cast")
                {
                    var stage = JsonSerializer.SerializeToNode(entry, ManifestInputs.JsonOptions)!.AsObject();
                    stage["file"] = relativeFile;
                    var definition = stage.Deserialize<StageManifestEntry>(ManifestInputs.JsonOptions)!;
                    if (entry.Category == "showroom-stage") AssetValidator.ValidateStageArea(definition.StageArea ?? throw new ManifestValidationException("Backdrop stageArea is missing."));
                    stages.Add(definition);
                }
                else
                {
                    foreach (var property in entry.CategoryProperties ?? [])
                    {
                        if (asset.ContainsKey(property.Key)) throw new ManifestValidationException($"{entry.AssetId}: metadata overrides generated field {property.Key}.");
                        asset[property.Key] = JsonNode.Parse(property.Value.GetRawText());
                    }
                }
                assets.Add(asset);
            }
            if ((tools.Count > 0 || stages.Any(stage => stage.Category == "showroom-cast")) && stages.Count(stage => stage.Category == "showroom-stage") != 1)
                throw new ManifestValidationException("Active targets or cast require exactly one validated showroom backdrop.");
            var manifest = new AssetManifest
            {
                ManifestVersion = "1.0.0", ConfigVersion = Hash(File.ReadAllBytes(configurationPath)), Tools = tools, AudioPools = pools, Stage = stages,
                SpawnProfiles = [new() { SpawnProfileId = "stage_default", MaxConcurrentTargets = gameplay.MaxConcurrentTargets,
                    MaxStageCoverage = gameplay.MaxStageCoverage, MinimumTargetSpacingFactor = gameplay.MinTargetSpacingFactor,
                    EntryEdges = ["left", "right"], DifficultyRampId = "ramp_default" }]
            };
            var document = JsonSerializer.SerializeToNode(manifest, ManifestInputs.JsonOptions)!.AsObject();
            document["assets"] = new JsonArray(assets.Select(asset => (JsonNode)asset).ToArray());
            document["planned"] = new JsonArray(planned.Select(asset => (JsonNode)asset).ToArray());
            document["scoreProfiles"] = JsonSerializer.SerializeToNode(new[] { "laser_default", "saw_default" }.Select(profile => new
            {
                scoreProfileId = profile, correctScore = gameplay.CorrectScore, wrongSharpeningPenalty = gameplay.WrongSharpeningPenalty,
                shaftHitPenalty = gameplay.ShaftHitPenalty, missPenalty = gameplay.MissPenalty
            }), ManifestInputs.JsonOptions);
            document["generation"] = new JsonObject { ["tool"] = "vgtools" };
            document["generation"]!["contentChecksum"] = Hash(Encoding.UTF8.GetBytes(ManifestInputs.CanonicalJson(document)));
            var manifestJson = ManifestInputs.CanonicalJson(document);
            var markdown = CreateMarkdown(assets, planned, pools);
            var registerJson = ManifestInputs.CanonicalJson(new AssetIdRegister { Version = 1, Entries = register.Values.OrderBy(entry => entry.AssetId, StringComparer.Ordinal).ToArray() });
            ValidateExistingOutput(paths.OutputFile);
            if (write)
            {
                WriteOutput(paths.OutputFile, manifestJson);
                WriteOutput(Path.ChangeExtension(paths.OutputFile, ".md"), markdown);
                WriteOutput(registerPath, registerJson);
                return new(0, issues, true);
            }
            foreach (var (filename, expected) in new[] { (paths.OutputFile, manifestJson), (Path.ChangeExtension(paths.OutputFile, ".md"), markdown), (registerPath, registerJson) })
            {
                if (!File.Exists(filename)) issues.Add(new("manifest", $"Generated file is missing: {Path.GetFileName(filename)}.", 3));
                else if (File.ReadAllText(filename) != expected) issues.Add(new("manifest", $"Generated file is stale: {Path.GetFileName(filename)}.", 2));
            }
            return new(issues.Any(issue => issue.ExitCode == 2) ? 2 : issues.Count > 0 ? 3 : 0, issues, false);
        }
        catch (Exception exception) when (exception is ManifestValidationException or JsonException or Json.Schema.JsonSchemaException
            or NetTopologySuite.Geometries.TopologyException or ArgumentException or InvalidOperationException)
        {
            issues.Add(new("manifest", exception.Message, 2));
            return new(2, issues, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new("manifest", exception.Message, 3));
            return new(3, issues, false);
        }
    }

    private static ToolManifestEntry CreateTool(AssetMetadata metadata, AssetZoneAuthoring zones, string visualFile,
        IReadOnlyList<AudioPoolManifestEntry> pools, IReadOnlyDictionary<string, AssetMetadata> assets)
    {
        var mode = metadata.Interaction == "tap" ? "laser" : "saw";
        foreach (var state in metadata.States!)
            if (!assets.Values.Any(asset => asset.Category == "tool-overlay" && GetProperty<string>(asset, "state") == state
                && GetProperty<string>(asset, "family") is var family && (family == "generic" || family == metadata.ToolType)))
                throw new ManifestValidationException($"{metadata.AssetId}: state overlay is missing for {state}.");
        if (!pools.Any(pool => pool.PoolId == mode + "_success") || !pools.Any(pool => pool.PoolId == mode + "_fail"))
            throw new ManifestValidationException($"{metadata.AssetId}: audio pool references are missing; audio derivation is required.");
        var angle = GetProperty<double?>(metadata, "cutDirectionDeg");
        var stroke = GetProperty<double?>(metadata, "minStrokeLengthNormalized");
        if (metadata.Interaction == "swipe" && (angle is null || !double.IsFinite(angle.Value) || stroke is null || !double.IsFinite(stroke.Value) || stroke <= 0 || stroke > 1))
            throw new ManifestValidationException($"{metadata.AssetId}: swipe direction or minimum stroke is missing/invalid.");
        return new() { ToolId = metadata.AssetId, Category = metadata.Category, ToolType = metadata.ToolType!, Interaction = metadata.Interaction!,
            SizeClass = metadata.SizeClass!, Pkd = metadata.Pkd!.Value, VisualAssetId = visualFile, Pivot = zones.Pivot, HitZones = zones.HitZones,
            CutDirectionDegrees = angle, MinimumStrokeLengthNormalized = stroke, StateOverlays = metadata.States!, ScoreProfileId = mode + "_default",
            AudioPools = new() { Success = mode + "_success", Fail = mode + "_fail" }, LicenseStatus = metadata.LicenseStatus };
    }

    private static T? GetProperty<T>(AssetMetadata metadata, string name) => metadata.CategoryProperties?.TryGetValue(name, out var value) == true
        ? value.Deserialize<T>(ManifestInputs.JsonOptions) : default;

    private static string FindImage(string assetsDirectory, AssetMetadata metadata, JsonObject template)
    {
        var norm = template["x-imageNorm"]!;
        var directory = Path.GetFullPath(Path.Combine(assetsDirectory, norm["directory"]!.GetValue<string>()));
        if (!directory.StartsWith(Path.GetFullPath(assetsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ManifestValidationException("Template image directory escapes Assets.");
        var matches = norm["formats"]!.AsArray().Select(format => Path.Combine(directory, metadata.AssetId + "." + format!.GetValue<string>())).Where(File.Exists).ToArray();
        if (matches.Length == 0) throw new FileNotFoundException($"{metadata.AssetId}: runtime image is missing.");
        if (matches.Length != 1) throw new ManifestValidationException($"{metadata.AssetId}: ambiguous runtime images.");
        return matches[0];
    }

    private static void ValidateRegisteredFiles(string assetsDirectory, IReadOnlyDictionary<string, AssetIdReservation> register)
    {
        foreach (var directory in new[] { "images", "animations" })
        {
            var fullDirectory = Path.Combine(assetsDirectory, directory);
            if (!Directory.Exists(fullDirectory)) continue;
            foreach (var filename in Directory.GetFiles(fullDirectory, "*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(filename) is not (".png" or ".webp" or ".json")) continue;
                var assetId = Path.GetFileNameWithoutExtension(filename);
                if (!register.ContainsKey(assetId)) throw new ManifestValidationException($"{assetId}: runtime file has no registered ID.");
            }
        }
    }

    private static void ValidateAudioPools(string assetsDirectory, IReadOnlyList<AudioPoolManifestEntry> pools)
    {
        if (pools.Select(pool => pool.PoolId).Distinct(StringComparer.Ordinal).Count() != pools.Count)
            throw new ManifestValidationException("Duplicate audio pool IDs.");
        foreach (var pool in pools)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { pool.PoolId };
            var fallbackId = pool.Fallback;
            while (fallbackId != "generated_tone")
            {
                if (!visited.Add(fallbackId)) throw new ManifestValidationException($"{pool.PoolId}: cyclic fallback references.");
                var fallback = pools.FirstOrDefault(candidate => candidate.PoolId == fallbackId)
                    ?? throw new ManifestValidationException($"{pool.PoolId}: unknown fallback.");
                fallbackId = fallback.Fallback;
            }
            if (pool.Fallback != "generated_tone" && !pools.Any(fallback => fallback.PoolId == pool.Fallback))
                throw new ManifestValidationException($"{pool.PoolId}: unknown fallback.");
            foreach (var variant in pool.Variants)
            {
                var directory = Path.GetFullPath(Path.Combine(assetsDirectory, "audio"));
                var filename = Path.GetFullPath(Path.Combine(directory, variant));
                if (!filename.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ManifestValidationException($"{pool.PoolId}: unsafe audio path.");
                if (!File.Exists(filename)) throw new FileNotFoundException($"{pool.PoolId}: missing audio variant.");
            }
        }
    }

    private static void ValidateExistingOutput(string filename)
    {
        if (!File.Exists(filename)) return;
        var existing = JsonNode.Parse(File.ReadAllText(filename))!.AsObject();
        var generation = existing["generation"]?.AsObject();
        if (generation?["tool"]?.GetValue<string>() != "vgtools" || generation["contentChecksum"] is null)
            throw new ManifestValidationException("Existing manifest is not a protected vgtools output; refusing overwrite.");
        var checksum = generation["contentChecksum"]!.GetValue<string>();
        generation.Remove("contentChecksum");
        if (Hash(Encoding.UTF8.GetBytes(ManifestInputs.CanonicalJson(existing))) != checksum)
            throw new ManifestValidationException("Generated manifest was manually changed; refusing overwrite.");
    }

    private static string CreateMarkdown(IReadOnlyList<JsonObject> assets, IReadOnlyList<JsonObject> planned, IReadOnlyList<AudioPoolManifestEntry> pools)
    {
        var text = new StringBuilder("# Generiertes Asset-Manifest\n\nDiese Datei wird durch vgtools erzeugt; Änderungen erfolgen ausschließlich in den Autorenquellen.\n\n| ID | Kategorie | Status | Datei |\n| --- | --- | --- | --- |\n");
        foreach (var entry in assets.Concat(planned).OrderBy(entry => entry["assetId"]!.GetValue<string>(), StringComparer.Ordinal))
            text.Append($"| {entry["assetId"]!.GetValue<string>()} | {entry["category"]!.GetValue<string>()} | {(entry["file"] is null ? "geplant" : "validiert")} | {entry["file"]?.GetValue<string>() ?? "noch nicht geliefert"} |\n");
        text.Append("\n## Audiopools\n\n| Pool | Kategorie | Varianten | Fallback | Dateien |\n| --- | --- | --- | --- | --- |\n");
        foreach (var pool in pools)
            text.Append($"| {pool.PoolId} | {pool.Category} | {pool.Variants.Count} | {pool.Fallback} | {string.Join(", ", pool.Variants)} |\n");
        return text.ToString();
    }

    private static void WriteOutput(string filename, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filename))!);
        var temporary = filename + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, filename, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}