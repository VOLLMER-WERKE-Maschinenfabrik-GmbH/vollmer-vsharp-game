using System.Text.Json;
using VGame.Contracts;

namespace VGame.Tools.Manifest.Tests;

[TestClass]
public sealed class AudioPoolBuilderTests
{
    [TestMethod]
    public void Derivation_RelatedGroupsAndSupportedFormatsRemainSeparate()
    {
        // Prüft exakt getrennte Dateigruppen mit gemeinsamen Präfixen und allen unterstützten Audioformaten.
        using var fixture = new AudioFixture();
        var first = CreateDefinition();
        var second = first with { PoolId = "slow_voice", FileGroup = "cheer_high_slow" };
        File.WriteAllText(fixture.DefinitionFile, ManifestInputs.CanonicalJson(new AudioPoolAuthoring { Pools = [first, second] }));
        fixture.AddFile("laser/cheer_high_01.mp3");
        fixture.AddFile("laser/cheer_high_02.ogg");
        fixture.AddFile("laser/cheer_high_slow_01.wav");
        var pools = AudioPoolBuilder.Read(fixture.Assets);
        Assert.HasCount(2, pools);
        Assert.HasCount(2, pools[0].Variants);
        Assert.AreEqual("laser/cheer_high_01.mp3", pools[0].Variants[0]);
        Assert.AreEqual("laser/cheer_high_02.ogg", pools[0].Variants[1]);
        Assert.AreEqual("laser/cheer_high_slow_01.wav", Assert.ContainsSingle(pools[1].Variants));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    [DataRow(8)]
    public void Derivation_VariablePoolSizesAreStableAndUseExistingRelativeFiles(int count)
    {
        // Prüft variable Poolgrößen und stabile Nummernreihenfolge unabhängig von der Erstellungsreihenfolge.
        using var fixture = new AudioFixture();
        for (var number = count; number > 0; number--) fixture.AddFile($"laser/cheer_high_{number:00}.wav");
        var pools = AudioPoolBuilder.Read(fixture.Assets);
        var pool = Assert.ContainsSingle(pools);
        Assert.HasCount(count, pool.Variants);
        Assert.AreEqual("laser/cheer_high_01.wav", pool.Variants[0]);
        Assert.AreEqual($"laser/cheer_high_{count:00}.wav", pool.Variants[^1]);
        Assert.AreEqual(ManifestInputs.CanonicalJson(pools), ManifestInputs.CanonicalJson(AudioPoolBuilder.Read(fixture.Assets)));
        foreach (var variant in pool.Variants) Assert.IsTrue(File.Exists(Path.Combine(fixture.Assets, "audio", variant)));
    }

    [TestMethod]
    public void Derivation_EmptyPoolRetainsFallbackWithoutCreatingVariants()
    {
        // Prüft einen nicht gelieferten Pool ohne künstliche Varianten und mit unverändertem Ersatzton-Verweis.
        using var fixture = new AudioFixture();
        var pool = Assert.ContainsSingle(AudioPoolBuilder.Read(fixture.Assets));
        Assert.IsEmpty(pool.Variants);
        Assert.AreEqual("generated_tone", pool.Fallback);
        Directory.Delete(Path.Combine(fixture.Assets, "audio"), true);
        Assert.IsEmpty(Assert.ContainsSingle(AudioPoolBuilder.Read(fixture.Assets)).Variants);
    }

    [TestMethod]
    [DataRow("cheer_high_bad.wav")]
    [DataRow("cheer_high_00.wav")]
    [DataRow("cheer_high_1.wav")]
    [DataRow("cheer_high_01.flac")]
    public void Derivation_MalformedVariantNamesAreRejected(string filename)
    {
        // Prüft ungültige Nummern, Suffixe und nicht unterstützte Formate für eine bekannte Dateigruppe.
        using var fixture = new AudioFixture();
        fixture.AddFile("laser/" + filename);
        Assert.ThrowsExactly<ManifestValidationException>(() => AudioPoolBuilder.Read(fixture.Assets));
    }

    [TestMethod]
    public void Derivation_DuplicateVariantNumbersAcrossFoldersOrFormatsAreRejected()
    {
        // Prüft mehrdeutige Varianten mit gleicher Nummer statt zufälliger Dateiauswahl.
        using var fixture = new AudioFixture();
        fixture.AddFile("laser/cheer_high_01.wav");
        fixture.AddFile("other/cheer_high_01.mp3");
        Assert.ThrowsExactly<ManifestValidationException>(() => AudioPoolBuilder.Read(fixture.Assets));
    }

    [TestMethod]
    public void Derivation_ExplicitFileListsAndMissingFieldsAreRejected()
    {
        // Prüft die Trennung zwischen kleinen Autorenwerten und ausschließlich abgeleiteten Dateilisten.
        using var fixture = new AudioFixture();
        File.WriteAllText(fixture.DefinitionFile, """{"pools":[{"poolId":"voice","fileGroup":"cheer_high","category":"laser-sfx","volumeProfile":"voice","bagRule":"shuffle_no_repeat","fallback":"generated_tone","variants":[]}]}""");
        Assert.ThrowsExactly<JsonException>(() => AudioPoolBuilder.Read(fixture.Assets));
        File.WriteAllText(fixture.DefinitionFile, """{"pools":[{"poolId":"voice"}]}""");
        Assert.ThrowsExactly<JsonException>(() => AudioPoolBuilder.Read(fixture.Assets));
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("unknown-fallback")]
    [DataRow("cycle")]
    [DataRow("unsafe-group")]
    [DataRow("null-pool")]
    public void Derivation_InvalidPoolDefinitionsAreRejected(string scenario)
    {
        // Prüft ungültige IDs, Fallback-Ketten und Pfadfragmente vor jeder Dateiableitung.
        using var fixture = new AudioFixture();
        var definition = CreateDefinition();
        IReadOnlyList<AudioPoolDefinition> definitions = scenario switch
        {
            "duplicate" => [definition, definition],
            "unknown-fallback" => [definition with { Fallback = "missing" }],
            "cycle" => [definition with { Fallback = definition.PoolId }],
            "unsafe-group" => [definition with { FileGroup = "../cheer_high" }],
            _ => [null!]
        };
        Assert.ThrowsExactly<ManifestValidationException>(() => AudioPoolBuilder.Build(Path.Combine(fixture.Assets, "audio"), new() { Pools = definitions }));
    }

    private static AudioPoolDefinition CreateDefinition() => new()
    {
        PoolId = "laser_success", FileGroup = "cheer_high", Category = "laser-sfx", VolumeProfile = "voice", BagRule = "shuffle_no_repeat", Fallback = "generated_tone"
    };

    private sealed class AudioFixture : IDisposable
    {
        public string Assets { get; } = Path.Combine(Path.GetTempPath(), "vgtools-audio-" + Guid.NewGuid().ToString("N"), "Assets");
        public string DefinitionFile => Path.Combine(Assets, "config/audio.pools.json");

        public AudioFixture()
        {
            Directory.CreateDirectory(Path.Combine(Assets, "config"));
            Directory.CreateDirectory(Path.Combine(Assets, "audio"));
            File.WriteAllText(DefinitionFile, ManifestInputs.CanonicalJson(new AudioPoolAuthoring { Pools = [CreateDefinition()] }));
        }

        public void AddFile(string relativePath)
        {
            var filename = Path.Combine(Assets, "audio", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
            File.WriteAllBytes(filename, [1, 2, 3]);
        }

        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Assets)!, true);
    }
}