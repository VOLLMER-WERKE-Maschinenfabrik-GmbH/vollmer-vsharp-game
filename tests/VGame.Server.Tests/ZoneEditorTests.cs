#if DEBUG
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Security;
using VGame.Server.Services;

namespace VGame.Server.Tests;

[TestClass]
[TestCategory("Server")]
public sealed class ZoneEditorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("Production", true, null)]
    [DataRow("Development", false, null)]
    [DataRow("Development", true, "https://example.invalid")]
    public async Task ZoneEditor_DeniesProductionRemoteAndForeignOrigin(string environment, bool local, string? origin)
    {
        // Prüft Entwicklungs-/Loopback-/Origin-Schutz vor Lese- oder Schreibzugriff auf Autorenquellen.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureTestServices(services => { services.RemoveAll<IClientOriginPolicy>(); services.AddSingleton<IClientOriginPolicy>(new TestPolicy(local)); });
        });
        using var client = factory.CreateClient();
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        using var response = await client.GetAsync("/tools/zones/api/assets", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        using var page = await client.GetAsync("/client/dist/zones.html", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NotFound, page.StatusCode);
    }

    [TestMethod]
    public async Task ZoneEditor_SavesOnlyValidZonesAndRejectsStaleVersions()
    {
        // Prüft Rundlauf, ungültige Speicherversuche und Versionskonflikte ohne Änderung von Bildern oder Metadaten.
        using var fixture = new EditorFixture();
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var asset = await client.GetFromJsonAsync<ZoneEditorAsset>("/tools/zones/api/asset/tool_0001", TestContext.CancellationToken);
        Assert.IsNotNull(asset);
        var imageBefore = File.ReadAllBytes(fixture.ImageFile);
        using var invalid = await client.PostAsJsonAsync("/tools/zones/api/asset/tool_0001", new { assetId = "tool_0001" }, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.IsFalse(File.Exists(fixture.ZoneFile));
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "new");
        using var saved = await client.PostAsJsonAsync("/tools/zones/api/asset/tool_0001", fixture.Zones, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        var roundTrip = await client.GetFromJsonAsync<ZoneEditorAsset>("/tools/zones/api/asset/tool_0001", TestContext.CancellationToken);
        Assert.IsNotNull(roundTrip);
        Assert.HasCount(2, roundTrip.Zones.HitZones);
        Assert.AreEqual(0.5, roundTrip.Zones.Pivot.X);
        using var stale = await client.PostAsJsonAsync("/tools/zones/api/asset/tool_0001", fixture.Zones, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        using var preview = await client.GetAsync("/tools/zones/api/preview?assetId=tool_0001&mode=swipe&x=0.55&y=0.5&endX=0.75&endY=0.5", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync(TestContext.CancellationToken));
        Assert.AreEqual("success", previewJson.RootElement.GetProperty("outcome").GetString());
        Assert.IsTrue(imageBefore.SequenceEqual(File.ReadAllBytes(fixture.ImageFile)));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(fixture.ZoneFile)!));
    }

    [TestMethod]
    public async Task ZoneEditor_PreviewUsesDomainOutcomeWithoutSavingOrChangingLiveRound()
    {
        // Prüft Laser-/Swipe-Vorhersage und Schaftvorrang in einer isolierten Domäneninstanz ohne Speichern.
        using var fixture = new EditorFixture();
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var before = engine.GetSnapshot();
        foreach (var (mode, points, expected) in new[]
        {
            ("tap", new[] { new Point2(0.7, 0.5) }, Outcome.Success),
            ("tap", new[] { new Point2(0.5, 0.5) }, Outcome.ShaftHit),
            ("swipe", new[] { new Point2(0.55, 0.5), new Point2(0.75, 0.5) }, Outcome.Success)
        })
        {
            using var response = await client.PostAsJsonAsync("/tools/zones/api/preview", new ZonePreviewRequest(fixture.Zones, mode, points), TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var outcome = await response.Content.ReadFromJsonAsync<ActionResolved>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { Converters = { new OutcomeJsonConverter(), new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }, TestContext.CancellationToken);
            Assert.IsNotNull(outcome);
            Assert.AreEqual(expected, outcome.Outcome);
        }
        Assert.AreEqual(before.RoundId, engine.GetSnapshot().RoundId);
        Assert.AreEqual(before.Score, engine.GetSnapshot().Score);
        Assert.IsFalse(File.Exists(fixture.ZoneFile));
    }

    private sealed class TestPolicy(bool local) : IClientOriginPolicy { public bool IsLocal(Microsoft.AspNetCore.Http.HttpContext? context) => local; }

    private sealed class EditorFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "vgame-zones-" + Guid.NewGuid().ToString("N"));
        public string ImageFile => Path.Combine(root, "Assets/images/tools/tool_0001.png");
        public string ZoneFile => Path.Combine(root, "media-source/zone-authoring/tool_0001.zones.json");
        public AssetZoneAuthoring Zones { get; } = new()
        {
            AssetId = "tool_0001", Canvas = new() { Width = 16, Height = 16 }, Pivot = new(0.5, 0.5),
            HitZones = [new() { ZoneId = "shaft", Role = "shaft", Points = [[0.25,0.25],[0.5,0.25],[0.5,0.75],[0.25,0.75]] },
                new() { ZoneId = "cut", Role = "cut", Points = [[0.5,0.25],[0.75,0.25],[0.75,0.75],[0.5,0.75]] }]
        };
        public EditorFixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ImageFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(ZoneFile)!);
            using var bitmap = new SKBitmap(new SKImageInfo(16, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            bitmap.Erase(SKColors.Transparent);
            for (var horizontal = 4; horizontal < 12; horizontal++)
            for (var vertical = 4; vertical < 12; vertical++) bitmap.SetPixel(horizontal, vertical, SKColors.White);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(ImageFile);
            data.SaveTo(stream);
        }
        public WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClientOriginPolicy>();
                services.AddSingleton<IClientOriginPolicy>(new TestPolicy(true));
                services.RemoveAll<ZoneEditorService>();
                services.AddSingleton(new ZoneEditorService(Path.Combine(root, "Assets"), Path.Combine(root, "media-source"), [], new GameplayOptions()));
            });
        });
        public void Dispose() => Directory.Delete(root, true);
    }
}
#endif