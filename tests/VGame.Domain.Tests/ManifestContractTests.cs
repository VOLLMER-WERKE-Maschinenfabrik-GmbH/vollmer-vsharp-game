using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.VisualBasic.FileIO;
using VGame.Contracts;
using System.Text.Json.Serialization;

namespace VGame.Domain.Tests;

[TestClass]
[TestCategory("Domain")]
public sealed class ManifestContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string ToolJson = """
        {"toolId":"tool_0001","category":"tool-image","toolType":"drill","interaction":"tap","sizeClass":"small","pkd":false,
        "visualAssetId":"tool_0001.webp","pivot":{"x":0.5,"y":0.5},
        "hitZones":[{"zoneId":"cut","role":"cut","points":[[0.3,0.1],[0.7,0.1],[0.5,0.4]]},{"zoneId":"shaft","role":"shaft","points":[[0.3,0.4],[0.7,0.4],[0.5,0.9]]}],
        "cutDirectionDeg":90,"minStrokeLengthNormalized":0.18,"stateOverlays":["dull","in_progress","sharp","failed"],
        "scoreProfileId":"laser_default","audioPools":{"success":"laser_success","fail":"laser_fail"},"licenseStatus":"royaltyfree"}
        """;

    [TestMethod]
    public void Contracts_ToolManifestRoundTripPreservesCanonicalFieldsAndCoordinatePairs()
    {
        // Prüft die unveränderten Feldnamen und Polygon-Zahlenpaare statt der Point2-Objekte aus dem Gestenprotokoll.
        var entry = JsonSerializer.Deserialize<ToolManifestEntry>(ToolJson, SerializerOptions)!;
        Assert.AreEqual(90, entry.CutDirectionDegrees);
        Assert.AreEqual(0.18, entry.MinimumStrokeLengthNormalized);
        Assert.AreEqual(0.3, entry.HitZones[0].Points[0][0]);
        var serialized = JsonSerializer.Serialize(entry, SerializerOptions);
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(ToolJson), JsonNode.Parse(serialized)));
        Assert.DoesNotContain("cutDirectionDegrees", serialized);
        Assert.DoesNotContain("minimumStrokeLengthNormalized", serialized);
    }

    [TestMethod]
    [DataRow("toolId")]
    [DataRow("pkd")]
    [DataRow("pivot")]
    [DataRow("hitZones")]
    [DataRow("visualAssetId")]
    [DataRow("stateOverlays")]
    [DataRow("licenseStatus")]
    public void Contracts_MissingToolFieldsAreRejected(string field)
    {
        // Prüft Pflichtfelder einschließlich des booleschen PKD-Werts ohne stillen Standardwert.
        var json = JsonNode.Parse(ToolJson)!.AsObject();
        json.Remove(field);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<ToolManifestEntry>(json.ToJsonString(), SerializerOptions));
    }

    [TestMethod]
    public void Contracts_StageAndAdditionalCategoriesRoundTripWithoutSchemaRenaming()
    {
        // Prüft w/h, cooldownMs und minTargetSpacingFactor sowie den verlustfreien Erhalt weiterer generierter Kategorien.
        const string json = """
            {"manifestVersion":"1.0.0","configVersion":"test-version","tools":[],"audioPools":[],
            "stage":[{"assetId":"bg_0001","category":"showroom-stage","file":"images/background/bg_0001.webp","layer":"backdrop","stageArea":{"x":0.05,"y":0.28,"w":0.9,"h":0.64}},{"assetId":"cast_0001","category":"showroom-cast","file":"images/background/cast_0001.png","layer":"props","cooldownMs":25000,"enabled":false}],
            "spawnProfiles":[{"spawnProfileId":"stage_default","maxConcurrentTargets":6,"maxStageCoverage":0.45,"minTargetSpacingFactor":1.25,"entryEdges":["left","right"],"difficultyRampId":"ramp_default"}],
            "effects":[{"assetId":"fx_0001"}]}
            """;
        var manifest = JsonSerializer.Deserialize<AssetManifest>(json, SerializerOptions)!;
        Assert.AreEqual(0.9, manifest.Stage[0].StageArea!.Width);
        Assert.AreEqual(25000, manifest.Stage[1].CooldownMilliseconds);
        Assert.IsFalse(manifest.Stage[1].Enabled);
        Assert.AreEqual(1.25, manifest.SpawnProfiles[0].MinimumTargetSpacingFactor);
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(manifest, SerializerOptions)));
    }

    [TestMethod]
    public void Contracts_RegisterAndZoneAuthoringUseCanonicalFileShapes()
    {
        // Prüft den typisierten Registerabruf und die vom Zonen-Editor vorgesehene Form ohne Änderung des Prototypkatalogs.
        var register = JsonSerializer.Deserialize<AssetIdRegister>(File.ReadAllText(Path.Combine(GetRepositoryRoot(), "Assets/config/id-register.json")), SerializerOptions)!;
        Assert.HasCount(37, register.Entries);
        const string json = """
            {"assetId":"tool_0001","canvas":{"width":1024,"height":1024},"pivot":{"x":0.5,"y":0.5},"hitZones":[{"zoneId":"cut","role":"cut","points":[[0.3,0.1],[0.7,0.1],[0.5,0.4]]}]}
            """;
        var zones = JsonSerializer.Deserialize<AssetZoneAuthoring>(json, SerializerOptions)!;
        Assert.AreEqual(1024, zones.Canvas.Width);
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(zones, SerializerOptions)));
    }

    [TestMethod]
    public void Authoring_RegisterMatchesDeliveryListAndKnownTemplates()
    {
        // Prüft die 37 eindeutigen Reservierungen gegen die strukturierte Lieferliste und vorhandene Kategorievorlagen.
        var repositoryRoot = GetRepositoryRoot();
        var register = JsonNode.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "Assets/config/id-register.json")))!;
        Assert.AreEqual(1, register["version"]!.GetValue<int>());
        var entries = register["entries"]!.AsArray();
        Assert.HasCount(37, entries);
        var categories = entries.ToDictionary(entry => entry!["assetId"]!.GetValue<string>(), entry => entry!["category"]!.GetValue<string>(), StringComparer.Ordinal);
        using var parser = new TextFieldParser(Path.Combine(repositoryRoot, "media-source/plan/delivery-list.csv"));
        parser.SetDelimiters(",");
        Assert.AreEqual("assetId", parser.ReadFields()![0]);
        var deliveredIds = new HashSet<string>(StringComparer.Ordinal);
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields()!;
            Assert.HasCount(7, fields);
            Assert.IsTrue(deliveredIds.Add(fields[0]));
            Assert.AreEqual(categories[fields[0]], fields[1]);
        }
        Assert.HasCount(37, deliveredIds);
        foreach (var entry in entries)
        {
            Assert.AreEqual("reserved", entry!["status"]!.GetValue<string>());
            Assert.MatchesRegex(@"^[a-z]+_[0-9]{4}$", entry["assetId"]!.GetValue<string>());
            var category = entry["category"]!.GetValue<string>();
            Assert.IsTrue(File.Exists(Path.Combine(repositoryRoot, $"Assets/templates/{category}.template.json")));
        }
        foreach (var directory in new[] { "media-source/assets", "media-source/zone-authoring", "Assets/images/tools", "Assets/images/overlays", "Assets/images/background", "Assets/animations/laser", "Assets/animations/grind" })
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(repositoryRoot, directory)), directory);
        }
    }

    [TestMethod]
    [DataRow("tool-image", "tool_0001", "{\"toolType\":\"drill\",\"interaction\":\"tap\",\"sizeClass\":\"small\",\"pkd\":false,\"states\":[\"dull\",\"in_progress\",\"sharp\",\"failed\"]}")]
    [DataRow("tool-overlay", "ovl_0001", "{\"family\":\"generic\",\"state\":\"dull\",\"anchor\":\"cut-zone\"}")]
    [DataRow("showroom-stage", "bg_0001", "{\"stageArea\":{\"x\":0.05,\"y\":0.28,\"w\":0.9,\"h\":0.64},\"layer\":\"backdrop\",\"parallaxFactor\":0,\"palette\":{\"floor\":\"test-floor\",\"wall\":\"test-wall\"},\"lightProfile\":\"test-light\"}")]
    [DataRow("showroom-cast", "cast_0001", "{\"sprite\":\"test-sprite\",\"layer\":\"props\",\"motionPath\":\"test-path\",\"speedProfile\":\"test-speed\",\"cooldownMs\":25000,\"enabled\":true}")]
    [DataRow("foreground-occluder", "occ_0001", "{\"motionProfileId\":\"pass_horizontal\"}")]
    [DataRow("laser-effect", "fx_0001", "{}")]
    [DataRow("grind-effect", "fx_0002", "{}")]
    public void Authoring_CategoryTemplateAcceptsCompleteMetadata(string category, string assetId, string fields)
    {
        // Prüft vollständige Metadaten jeder Kategorie gegen die tatsächlichen JSON-Schema-Dateien einschließlich gemeinsamer Referenz.
        var metadata = CreateMetadata(category, assetId, fields);
        Assert.IsTrue(EvaluateTemplate(category, metadata).IsValid);
        Assert.IsFalse(EvaluateTemplate(category, new JsonObject()).IsValid);
        metadata["category"] = "unknown";
        Assert.IsFalse(EvaluateTemplate(category, metadata).IsValid);
    }

    [TestMethod]
    [DataRow("licenseStatus")]
    [DataRow("source")]
    [DataRow("createdUtc")]
    public void Authoring_MissingProvenanceIsRejected(string field)
    {
        // Prüft, dass unvollständige Herkunfts- und Lizenzangaben nicht durch Standardwerte ergänzt werden.
        var metadata = CreateMetadata("laser-effect", "fx_0001", "{}");
        metadata.Remove(field);
        Assert.IsFalse(EvaluateTemplate("laser-effect", metadata).IsValid);
    }

    [TestMethod]
    public void Authoring_GeneratedMetadataRequiresModelSeedAndPrompt()
    {
        // Prüft die zusätzlichen Pflichtangaben für Leonardo-Herkunft ohne Erfindung von Produktionsdaten.
        var metadata = CreateMetadata("laser-effect", "fx_0001", "{}");
        metadata["source"] = "leonardo-ai";
        Assert.IsFalse(EvaluateTemplate("laser-effect", metadata).IsValid);
        metadata["model"] = "test-model";
        metadata["seed"] = 42;
        metadata["prompt"] = "test-prompt";
        Assert.IsTrue(EvaluateTemplate("laser-effect", metadata).IsValid);
        metadata["seed"] = -1;
        Assert.IsFalse(EvaluateTemplate("laser-effect", metadata).IsValid);
        metadata["createdUtc"] = "invalid";
        metadata["seed"] = 42;
        Assert.IsFalse(EvaluateTemplate("laser-effect", metadata).IsValid);
    }

    [TestMethod]
    [DataRow("[\"dull\",\"in_progress\",\"sharp\"]")]
    [DataRow("[\"dull\",\"dull\",\"sharp\",\"failed\"]")]
    [DataRow("[\"dull\",\"in_progress\",\"sharp\",\"unknown\"]")]
    public void Authoring_InvalidToolStatesAreRejected(string states)
    {
        // Prüft fehlende, doppelte und unbekannte Zustände gegen die verbindlichen vier Werkzeugzustände.
        var metadata = CreateMetadata("tool-image", "tool_0001", "{\"toolType\":\"drill\",\"interaction\":\"tap\",\"sizeClass\":\"small\",\"pkd\":false}");
        metadata["states"] = JsonNode.Parse(states);
        Assert.IsFalse(EvaluateTemplate("tool-image", metadata).IsValid);
    }

    private static JsonObject CreateMetadata(string category, string assetId, string fields)
    {
        var metadata = new JsonObject
        {
            ["assetId"] = assetId, ["category"] = category, ["licenseStatus"] = "royaltyfree",
            ["source"] = "test-fixture", ["createdUtc"] = "2026-10-13T10:00:00Z"
        };
        foreach (var field in JsonNode.Parse(fields)!.AsObject()) metadata[field.Key] = field.Value?.DeepClone();
        return metadata;
    }

    private static EvaluationResults EvaluateTemplate(string category, JsonNode metadata)
    {
        var templateDirectory = Path.Combine(GetRepositoryRoot(), "Assets/templates");
        var commonPath = Path.Combine(templateDirectory, "asset.common.schema.json");
        var buildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        _ = JsonSchema.FromFile(commonPath, buildOptions);
        var schema = JsonSchema.FromFile(Path.Combine(templateDirectory, $"{category}.template.json"), buildOptions);
        return schema.Evaluate(JsonSerializer.SerializeToElement(metadata), new EvaluationOptions { RequireFormatValidation = true });
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VGame.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}