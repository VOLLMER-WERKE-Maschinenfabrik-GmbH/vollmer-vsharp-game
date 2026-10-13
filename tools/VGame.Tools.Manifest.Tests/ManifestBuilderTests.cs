using VGame.Contracts;
using SkiaSharp;
using System.Text.Json.Nodes;

namespace VGame.Tools.Manifest.Tests;

[TestClass]
public sealed class ManifestBuilderTests
{
    [TestMethod]
    [DataRow("null-zone")]
    [DataRow("missing-pivot-coordinate")]
    [DataRow("covered-cut")]
    [DataRow("disconnected-cut")]
    public void Validation_InvalidZoneShapesAndUnreachableCutAreRejected(string scenario)
    {
        // Prüft fehlende Koordinaten, Null-Zonen und eine durch den Schaftvorrang vollständig unerreichbare Schneide.
        using var bitmap = CreateToolBitmap();
        var document = System.Text.Json.JsonSerializer.SerializeToNode(CreateToolZones(), ManifestInputs.JsonOptions)!;
        if (scenario == "null-zone") document["hitZones"]![0] = null;
        else if (scenario == "missing-pivot-coordinate") document["pivot"]!.AsObject().Remove("x");
        else if (scenario == "covered-cut") document["hitZones"]![0]!["points"] = JsonNode.Parse("[[0.25,0.25],[0.75,0.25],[0.75,0.75],[0.25,0.75]]");
        else
        {
            document["hitZones"]![0]!["points"] = JsonNode.Parse("[[0.45,0.25],[0.55,0.25],[0.55,0.75],[0.45,0.75]]");
            document["hitZones"]![1]!["points"] = JsonNode.Parse("[[0.25,0.25],[0.75,0.25],[0.75,0.75],[0.25,0.75]]");
        }
        var filename = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filename, document.ToJsonString());
            Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.ValidateZones(filename, CreateToolMetadata(), bitmap));
        }
        finally { File.Delete(filename); }
    }

    [TestMethod]
    public void Builder_AuthoredFieldsCannotOverwriteGeneratedRuntimePaths()
    {
        // Prüft, dass freie Metadatenfelder keine erzeugten Laufzeitpfade ersetzen dürfen.
        using var fixture = new ManifestFixture();
        fixture.AddCompleteTool();
        var filename = Path.Combine(fixture.Paths.SourceDirectory, "assets/ovl_0001.meta.json");
        var metadata = JsonNode.Parse(File.ReadAllText(filename))!;
        metadata["file"] = "../../outside.webp";
        File.WriteAllText(filename, metadata.ToJsonString());
        Assert.AreEqual(2, new ManifestBuilder(CreateAudioPools()).Run(fixture.Paths, true).ExitCode);
        Assert.IsFalse(File.Exists(fixture.Paths.OutputFile));
    }

    [TestMethod]
    public void Cli_BuildAndVerifyUseCanonicalPathsAndExitCodes()
    {
        // Prüft die echte CLI-Anbindung für erfolgreiche Ausgabe und schreibfreie Prüfung.
        using var fixture = new ManifestFixture();
        fixture.AddBackdrop();
        using var output = new StringWriter();
        var arguments = new[] { "manifest", "build", "--assets", fixture.Paths.AssetsDirectory, "--source", fixture.Paths.SourceDirectory };
        Assert.AreEqual(0, VGame.Tools.Cli.Program.Run(arguments, output));
        arguments[1] = "verify";
        Assert.AreEqual(0, VGame.Tools.Cli.Program.Run(arguments, output));
        Assert.Contains("files written: false", output.ToString());
    }

    [TestMethod]
    public void Cli_PlannedAssetsAndMissingSourcesReturnMissingFileCode()
    {
        // Prüft geplante Reservierungen und fehlende Quellen mit dem dokumentierten Exit-Code 3.
        using var fixture = new ManifestFixture();
        fixture.ReserveBackdrop();
        using var output = new StringWriter();
        var arguments = new[] { "manifest", "build", "--assets", fixture.Paths.AssetsDirectory, "--source", fixture.Paths.SourceDirectory };
        Assert.AreEqual(0, VGame.Tools.Cli.Program.Run(arguments, output));
        arguments[1] = "verify";
        Assert.AreEqual(3, VGame.Tools.Cli.Program.Run(arguments, output));
        File.Delete(Path.Combine(fixture.Paths.AssetsDirectory, "config/gameplay.json"));
        arguments[1] = "build";
        Assert.AreEqual(3, VGame.Tools.Cli.Program.Run(arguments, output));
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("build --unknown-option")]
    [DataRow("build --assets")]
    public void Cli_InvalidCommandsAndOptionsReturnValidationCode(string command)
    {
        // Prüft unbekannte Befehle und unvollständige Optionen ohne einen Build auszulösen.
        using var output = new StringWriter();
        Assert.AreEqual(2, VGame.Tools.Cli.Program.Run(new[] { "manifest" }.Concat(command.Split(' ')).ToArray(), output));
    }

    [TestMethod]
    public void Builder_CompleteToolReferencesValidatedImagesZonesOverlaysAndAudio()
    {
        // Prüft einen vollständig aktiven Werkzeugfall einschließlich aller Zustände, Scoreprofil und tatsächlicher Audioverweise.
        using var fixture = new ManifestFixture();
        fixture.AddCompleteTool();
        var builder = new ManifestBuilder(CreateAudioPools());
        var result = builder.Run(fixture.Paths, true);
        Assert.AreEqual(0, result.ExitCode, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Paths.OutputFile))!;
        var tool = Assert.ContainsSingle(manifest["tools"]!.AsArray());
        Assert.AreEqual("images/tools/tool_0001.webp", tool!["visualAssetId"]!.GetValue<string>());
        Assert.HasCount(4, tool["stateOverlays"]!.AsArray());
        Assert.AreEqual("laser_success", tool["audioPools"]!["success"]!.GetValue<string>());
        Assert.AreEqual(0, builder.Run(fixture.Paths, false).ExitCode);
    }

    [TestMethod]
    public void Builder_MissingZonesAndAudioReferencesAreRejected()
    {
        // Prüft fehlende Zonen und nicht abgeleitete Audiopools ohne Schreiben einer unvollständigen Ausgabe.
        using var fixture = new ManifestFixture();
        fixture.AddCompleteTool();
        Assert.AreEqual(2, new ManifestBuilder().Run(fixture.Paths, true).ExitCode);
        Assert.IsFalse(File.Exists(fixture.Paths.OutputFile));
        File.Delete(Path.Combine(fixture.Paths.SourceDirectory, "zone-authoring/tool_0001.zones.json"));
        Assert.AreEqual(3, new ManifestBuilder(CreateAudioPools()).Run(fixture.Paths, true).ExitCode);
        Assert.IsFalse(File.Exists(fixture.Paths.OutputFile));
    }

    [TestMethod]
    public void Builder_CyclicAndMissingAudioVariantsAreRejected()
    {
        // Prüft, dass Audio-Fallbacks keine Zyklen und keine toten Dateiverweise enthalten dürfen.
        using var fixture = new ManifestFixture();
        fixture.AddCompleteTool();
        var pools = CreateAudioPools().ToArray();
        pools[2] = pools[2] with { Fallback = "laser_success" };
        Assert.AreEqual(2, new ManifestBuilder(pools).Run(fixture.Paths, true).ExitCode);
        File.Delete(Path.Combine(fixture.Paths.AssetsDirectory, "audio/laser/test_01.wav"));
        Assert.AreEqual(3, new ManifestBuilder(CreateAudioPools()).Run(fixture.Paths, true).ExitCode);
    }

    private static IReadOnlyList<AudioPoolManifestEntry> CreateAudioPools() => new[] { "laser_success", "laser_fail", "laser_effect" }
        .Select(id => new AudioPoolManifestEntry { PoolId = id, Category = "laser-sfx", Variants = ["laser/test_01.wav"],
            VolumeProfile = "voice", BagRule = "shuffle_no_repeat", Fallback = id == "laser_effect" ? "generated_tone" : "laser_effect" }).ToArray();

    [TestMethod]
    public void Builder_RepeatedBuildIsIdenticalAndVerifyDoesNotWrite()
    {
        // Prüft echte generierte Ausgaben auf byteidentischen Rundlauf und schreibfreien Verify-Lauf.
        using var fixture = new ManifestFixture();
        fixture.AddBackdrop();
        var builder = new ManifestBuilder();
        Assert.AreEqual(0, builder.Run(fixture.Paths, true).ExitCode);
        var first = fixture.OutputSnapshot();
        Assert.AreEqual(0, builder.Run(fixture.Paths, true).ExitCode);
        Assert.AreEqual(first, fixture.OutputSnapshot());
        Assert.AreEqual(0, builder.Run(fixture.Paths, false).ExitCode);
        Assert.AreEqual(first, fixture.OutputSnapshot());
        Assert.IsNotEmpty(JsonNode.Parse(File.ReadAllText(fixture.Paths.OutputFile))!["assets"]!.AsArray());
    }

    [TestMethod]
    public void Builder_PlannedReservationsHaveNoDeadFilesAndVerifyFails()
    {
        // Prüft sichtbare geplante Reservierungen ohne erfundene Herkunft oder tote Laufzeitverweise.
        using var fixture = new ManifestFixture();
        fixture.ReserveBackdrop();
        var builder = new ManifestBuilder();
        var build = builder.Run(fixture.Paths, true);
        Assert.AreEqual(0, build.ExitCode);
        Assert.IsTrue(build.Written);
        var planned = Assert.ContainsSingle(JsonNode.Parse(File.ReadAllText(fixture.Paths.OutputFile))!["planned"]!.AsArray());
        Assert.IsNull(planned!["file"]);
        Assert.AreEqual("planned", planned["status"]!.GetValue<string>());
        Assert.AreEqual(3, builder.Run(fixture.Paths, false).ExitCode);
    }

    [TestMethod]
    public void Builder_InvalidInputAndManualEditsPreserveExistingOutput()
    {
        // Prüft den Schutz der letzten gültigen Ausgabe bei fehlerhaften Quellen und manuellen Manifeständerungen.
        using var fixture = new ManifestFixture();
        fixture.AddBackdrop();
        var builder = new ManifestBuilder();
        Assert.AreEqual(0, builder.Run(fixture.Paths, true).ExitCode);
        var original = fixture.OutputSnapshot();
        File.WriteAllText(Path.Combine(fixture.Paths.SourceDirectory, "assets/bg_0001.meta.json"), "{}");
        Assert.AreEqual(2, builder.Run(fixture.Paths, true).ExitCode);
        Assert.AreEqual(original, fixture.OutputSnapshot());
        fixture.AddBackdrop();
        var document = JsonNode.Parse(File.ReadAllText(fixture.Paths.OutputFile))!;
        document["configVersion"] = "manual-change";
        File.WriteAllText(fixture.Paths.OutputFile, document.ToJsonString());
        var edited = File.ReadAllText(fixture.Paths.OutputFile);
        Assert.AreEqual(2, builder.Run(fixture.Paths, true).ExitCode);
        Assert.AreEqual(edited, File.ReadAllText(fixture.Paths.OutputFile));
    }

    [TestMethod]
    public void Builder_MissingAssignedImageAndOrphanFileFailWithoutWriting()
    {
        // Prüft fehlende aktive Dateien und unbekannte Bild-IDs ohne Erzeugung einer Teil-Ausgabe.
        using var fixture = new ManifestFixture();
        fixture.AddBackdrop();
        File.Delete(Path.Combine(fixture.Paths.AssetsDirectory, "images/background/bg_0001.webp"));
        Assert.AreEqual(3, new ManifestBuilder().Run(fixture.Paths, true).ExitCode);
        Assert.IsFalse(File.Exists(fixture.Paths.OutputFile));
        fixture.AddBackdrop();
        File.WriteAllText(Path.Combine(fixture.Paths.AssetsDirectory, "images/background/unknown_0001.webp"), "invalid");
        Assert.AreEqual(2, new ManifestBuilder().Run(fixture.Paths, true).ExitCode);
        Assert.IsFalse(File.Exists(fixture.Paths.OutputFile));
    }

    [TestMethod]
    public void Builder_DoesNotModifyAuthoringSourcesOrImages()
    {
        // Prüft, dass ein Build Bilder und authored Metadaten bytegenau unverändert lässt.
        using var fixture = new ManifestFixture();
        fixture.AddBackdrop();
        var metadata = Path.Combine(fixture.Paths.SourceDirectory, "assets/bg_0001.meta.json");
        var image = Path.Combine(fixture.Paths.AssetsDirectory, "images/background/bg_0001.webp");
        var beforeMetadata = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(metadata)));
        var beforeImage = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(image)));
        Assert.AreEqual(0, new ManifestBuilder().Run(fixture.Paths, true).ExitCode);
        Assert.AreEqual(beforeMetadata, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(metadata))));
        Assert.AreEqual(beforeImage, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(image))));
    }

    private sealed class ManifestFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "vgtools-test-" + Guid.NewGuid().ToString("N"));
        public ManifestPaths Paths { get; }

        public ManifestFixture()
        {
            Paths = new(Path.Combine(root, "Assets"), Path.Combine(root, "media-source"), Path.Combine(root, "Assets/config/manifest.json"));
            foreach (var directory in new[] { "config", "templates", "images/background" }) Directory.CreateDirectory(Path.Combine(Paths.AssetsDirectory, directory));
            Directory.CreateDirectory(Path.Combine(Paths.SourceDirectory, "assets"));
            Directory.CreateDirectory(Path.Combine(Paths.SourceDirectory, "zone-authoring"));
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "VGame.sln"))) repository = repository.Parent;
            var canonicalTemplates = Path.Combine(repository!.FullName, "Assets/templates");
            foreach (var filename in Directory.GetFiles(canonicalTemplates, "*.json")) File.Copy(filename, Path.Combine(Paths.AssetsDirectory, "templates", Path.GetFileName(filename)));
            File.WriteAllText(Path.Combine(Paths.AssetsDirectory, "config/gameplay.json"), ManifestInputs.CanonicalJson(new VGame.Domain.GameplayOptions()));
            File.WriteAllText(Path.Combine(Paths.AssetsDirectory, "config/id-register.json"), """{"version":1,"entries":[]}""");
        }

        public void ReserveBackdrop() => File.WriteAllText(Path.Combine(Paths.AssetsDirectory, "config/id-register.json"), """{"version":1,"entries":[{"assetId":"bg_0001","category":"showroom-stage","status":"reserved"}]}""");

        public void AddBackdrop()
        {
            File.WriteAllText(Path.Combine(Paths.AssetsDirectory, "config/id-register.json"), """{"version":1,"entries":[{"assetId":"bg_0001","category":"showroom-stage","status":"assigned"}]}""");
            File.WriteAllText(Path.Combine(Paths.SourceDirectory, "assets/bg_0001.meta.json"), """
                {"assetId":"bg_0001","category":"showroom-stage","licenseStatus":"test","source":"test-fixture","createdUtc":"2026-10-13T10:00:00Z",
                "stageArea":{"x":0.05,"y":0.28,"w":0.9,"h":0.64},"layer":"backdrop","parallaxFactor":0,"palette":{"floor":"test","wall":"test"},"lightProfile":"test"}
                """);
            using var bitmap = new SKBitmap(2560, 1440);
            bitmap.Erase(SKColors.Gray);
            WriteBitmap(bitmap, Path.Combine(Paths.AssetsDirectory, "images/background/bg_0001.webp"));
        }

        public void AddCompleteTool()
        {
            AddBackdrop();
            Directory.CreateDirectory(Path.Combine(Paths.AssetsDirectory, "images/tools"));
            Directory.CreateDirectory(Path.Combine(Paths.AssetsDirectory, "images/overlays"));
            Directory.CreateDirectory(Path.Combine(Paths.AssetsDirectory, "audio/laser"));
            File.WriteAllBytes(Path.Combine(Paths.AssetsDirectory, "audio/laser/test_01.wav"), [1, 2, 3]);
            var register = ManifestInputs.ReadRegister(Path.Combine(Paths.AssetsDirectory, "config/id-register.json")).Values.ToList();
            var metadata = CreateToolMetadata();
            register.Add(new() { AssetId = metadata.AssetId, Category = metadata.Category, Status = "assigned" });
            File.WriteAllText(Path.Combine(Paths.SourceDirectory, "assets/tool_0001.meta.json"), ManifestInputs.CanonicalJson(metadata));
            using (var bitmap = CreateNormBitmap(1024)) WriteBitmap(bitmap, Path.Combine(Paths.AssetsDirectory, "images/tools/tool_0001.webp"));
            var zones = CreateToolZones() with { Canvas = new() { Width = 1024, Height = 1024 } };
            File.WriteAllText(Path.Combine(Paths.SourceDirectory, "zone-authoring/tool_0001.zones.json"), ManifestInputs.CanonicalJson(zones));
            foreach (var (state, index) in metadata.States!.Select((state, index) => (state, index)))
            {
                var assetId = $"ovl_{index + 1:0000}";
                register.Add(new() { AssetId = assetId, Category = "tool-overlay", Status = "assigned" });
                File.WriteAllText(Path.Combine(Paths.SourceDirectory, "assets", assetId + ".meta.json"), ManifestInputs.CanonicalJson(new
                {
                    assetId, category = "tool-overlay", licenseStatus = "test", source = "test-fixture", createdUtc = "2026-10-13T10:00:00Z", family = "generic", state, anchor = "cut-zone"
                }));
                using var overlay = CreateNormBitmap(256);
                WriteBitmap(overlay, Path.Combine(Paths.AssetsDirectory, "images/overlays", assetId + ".webp"));
            }
            File.WriteAllText(Path.Combine(Paths.AssetsDirectory, "config/id-register.json"), ManifestInputs.CanonicalJson(new AssetIdRegister { Version = 1, Entries = register }));
        }

        private static SKBitmap CreateNormBitmap(int size)
        {
            var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            bitmap.Erase(SKColors.Transparent);
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(size / 4, size / 4, size / 2, size / 2, paint);
            return bitmap;
        }

        public string OutputSnapshot() => string.Join("\n", new[] { Paths.OutputFile, Path.ChangeExtension(Paths.OutputFile, ".md"), Path.Combine(Paths.AssetsDirectory, "config/id-register.json") }.Select(File.ReadAllText));
        public void Dispose() => Directory.Delete(root, true);
    }

    [TestMethod]
    [DataRow(0.05, 0.28, 0.9, 0.64, true)]
    [DataRow(0.2, 0.1, 0.9, 0.7, false)]
    [DataRow(0.0, 0.0, 0.5, 0.5, false)]
    public void Validation_StageBoundsAndMinimumAreaAreEnforced(double horizontal, double vertical, double width, double height, bool valid)
    {
        // Prüft die fachliche Bühnenflächenregel einschließlich außerhalb liegender und zu kleiner Flächen.
        var area = new ManifestStageArea { HorizontalPosition = horizontal, VerticalPosition = vertical, Width = width, Height = height };
        if (valid) AssetValidator.ValidateStageArea(area);
        else Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.ValidateStageArea(area));
    }

    [TestMethod]
    public void Validation_ZonesCoverPixelsAndRetainShaftBoundaryPriority()
    {
        // Prüft Silhouettenabdeckung und den Schaftvorrang mit derselben Domänenwertung wie im Spiel.
        using var bitmap = CreateToolBitmap();
        var metadata = CreateToolMetadata();
        var zones = CreateToolZones();
        var filename = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filename, ManifestInputs.CanonicalJson(zones));
            var validated = AssetValidator.ValidateZones(filename, metadata, bitmap);
            Assert.AreEqual(HitZoneRole.Shaft, AssetValidator.Classify(validated, new Point2(0.5, 0.5))!.HitZone.Role);
            Assert.AreEqual(HitZoneRole.Cut, AssetValidator.Classify(validated, new Point2(0.75, 0.5))!.HitZone.Role);
            var gap = zones with { HitZones = [zones.HitZones[0], zones.HitZones[1] with { Points = [[0.8, 0.25], [0.8, 0.75], [0.9, 0.75], [0.9, 0.25]] }] };
            File.WriteAllText(filename, ManifestInputs.CanonicalJson(gap));
            Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.ValidateZones(filename, metadata, bitmap));
        }
        finally { File.Delete(filename); }
    }

    [TestMethod]
    public void Validation_RejectsSelfIntersectingAndOutOfBoundsPolygons()
    {
        // Prüft ungültige Polygongeometrie vor der pixelweisen Abdeckungsprüfung.
        var crossing = new ManifestHitZone { ZoneId = "cut", Role = "cut", Points = [[0, 0], [1, 1], [1, 0], [0, 1]] };
        Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.CreatePolygon(crossing, "tool_0001"));
        var outside = crossing with { Points = [[0, 0], [2, 0], [0, 1]] };
        Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.CreatePolygon(outside, "tool_0001"));
    }

    [TestMethod]
    public void Validation_ImageChecksDimensionsBorderAndEmptySilhouette()
    {
        // Prüft echte WebP-Dateien auf Normmaß, transparente Ränder und nicht leere Silhouette.
        var filename = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".webp");
        var template = JsonNode.Parse("""{"x-imageNorm":{"formats":["webp"],"width":16,"height":16,"transparent":true}}""")!.AsObject();
        using var bitmap = CreateToolBitmap();
        try
        {
            WriteBitmap(bitmap, filename);
            using var validated = AssetValidator.ValidateImage(filename, template);
            Assert.AreEqual(16, validated.Width);
            bitmap.SetPixel(0, 0, SKColors.White);
            WriteBitmap(bitmap, filename);
            Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.ValidateImage(filename, template));
            bitmap.Erase(SKColors.Transparent);
            WriteBitmap(bitmap, filename);
            Assert.ThrowsExactly<ManifestValidationException>(() => AssetValidator.ValidateImage(filename, template));
        }
        finally { File.Delete(filename); }
    }

    private static SKBitmap CreateToolBitmap()
    {
        var bitmap = new SKBitmap(new SKImageInfo(16, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent);
        for (var horizontal = 4; horizontal < 12; horizontal++)
        for (var vertical = 4; vertical < 12; vertical++) bitmap.SetPixel(horizontal, vertical, SKColors.White);
        return bitmap;
    }

    private static void WriteBitmap(SKBitmap bitmap, string filename)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 100);
        using var stream = File.Create(filename);
        data.SaveTo(stream);
    }

    private static AssetMetadata CreateToolMetadata() => new()
    {
        AssetId = "tool_0001", Category = "tool-image", LicenseStatus = "test", Source = "test", CreatedUtc = DateTimeOffset.UnixEpoch,
        ToolType = "drill", Interaction = "tap", SizeClass = "small", Pkd = false, States = ["dull", "in_progress", "sharp", "failed"]
    };

    private static AssetZoneAuthoring CreateToolZones() => new()
    {
        AssetId = "tool_0001", Canvas = new() { Width = 16, Height = 16 }, Pivot = new(0.5, 0.5),
        HitZones = [new() { ZoneId = "shaft", Role = "shaft", Points = [[0.25, 0.25], [0.5, 0.25], [0.5, 0.75], [0.25, 0.75]] },
            new() { ZoneId = "cut", Role = "cut", Points = [[0.5, 0.25], [0.75, 0.25], [0.75, 0.75], [0.5, 0.75]] }]
    };

    [TestMethod]
    [DataRow("reserved")]
    [DataRow("unknown")]
    public void Inputs_RejectReservedOrUnknownActiveAssignment(string status)
    {
        // Prüft, dass aktive Metadaten nur ausdrücklich zugewiesene IDs verwenden dürfen.
        var metadata = new AssetMetadata { AssetId = "tool_0001", Category = "tool-image", LicenseStatus = "test", Source = "test", CreatedUtc = DateTimeOffset.UnixEpoch };
        var register = new Dictionary<string, AssetIdReservation>
        {
            [metadata.AssetId] = new() { AssetId = metadata.AssetId, Category = metadata.Category, Status = status }
        };
        Assert.ThrowsExactly<ManifestValidationException>(() => ManifestInputs.ValidateAssignment(metadata, register));
    }

    [TestMethod]
    public void Inputs_RejectDuplicateIds()
    {
        // Prüft doppelte Registereinträge vor dem Schreiben irgendeiner generierten Datei.
        var filename = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filename, """{"version":1,"entries":[{"assetId":"tool_0001","category":"tool-image","status":"assigned"},{"assetId":"tool_0001","category":"tool-image","status":"assigned"}]}""");
            Assert.ThrowsExactly<ManifestValidationException>(() => ManifestInputs.ReadRegister(filename));
        }
        finally { File.Delete(filename); }
    }

    [TestMethod]
    [DataRow("images/manifest.json")]
    [DataRow("config/other.json")]
    [DataRow("../media-source/manifest.json")]
    public void Inputs_RejectOutputOutsideCanonicalConfiguration(string relativeOutput)
    {
        // Prüft den Schutz der Bilder und authored Dateien durch einen festen Ausgabepfad.
        var assets = Path.Combine(Path.GetTempPath(), "manifest-test", "Assets");
        var paths = new ManifestPaths(assets, Path.Combine(Path.GetTempPath(), "manifest-test", "media-source"), Path.Combine(assets, relativeOutput));
        Assert.ThrowsExactly<ManifestValidationException>(paths.Validate);
    }

    [TestMethod]
    public void Inputs_AcceptCanonicalOutputAndStableSerialization()
    {
        // Prüft den zulässigen Zielpfad und stabile JSON-Ausgabe ohne laufzeitabhängige Zeitstempel.
        var assets = Path.Combine(Path.GetTempPath(), "manifest-test", "Assets");
        new ManifestPaths(assets, Path.Combine(Path.GetTempPath(), "manifest-test", "media-source"), Path.Combine(assets, "config/manifest.json")).Validate();
        var entry = new AssetIdReservation { AssetId = "tool_0001", Category = "tool-image", Status = "reserved" };
        Assert.AreEqual(ManifestInputs.CanonicalJson(entry), ManifestInputs.CanonicalJson(entry));
    }
}