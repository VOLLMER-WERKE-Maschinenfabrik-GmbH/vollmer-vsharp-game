using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using VGame.Contracts;
using VGame.Server.Security;
using VGame.Server.Services;
using System.Net.Http.Json;
#if DEBUG
using SkiaSharp;
using VGame.Tools.Manifest;
#endif

namespace VGame.Server.Tests;

[TestClass]
[TestCategory("Server")]
public sealed class GameHubIntegrationTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task FaceImages_AllStateVariantsAreServedFromCanonicalAssets()
    {
        // Prüft die tatsächliche Auslieferung aller gelieferten Zustandsvarianten aus dem zentralen Laufzeitpfad.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/Assets/images/faces/catalog.json", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, catalog.RootElement.GetProperty("version").GetInt32());
        var expected = new Dictionary<string, int> { ["dull"] = 12, ["inProgress"] = 4, ["sharp"] = 8, ["failed"] = 8 };
        foreach (var state in catalog.RootElement.GetProperty("states").EnumerateObject())
        {
            Assert.AreEqual(expected[state.Name], state.Value.GetArrayLength());
            foreach (var filename in state.Value.EnumerateArray())
            {
                Assert.MatchesRegex("^face_[a-zA-Z0-9_]+\\.webp$", filename.GetString()!);
                using var image = await client.GetAsync("/Assets/images/faces/" + filename.GetString(), TestContext.CancellationToken);
                Assert.AreEqual(System.Net.HttpStatusCode.OK, image.StatusCode);
                Assert.AreEqual("image/webp", image.Content.Headers.ContentType?.MediaType);
                Assert.IsNotEmpty(await image.Content.ReadAsByteArrayAsync(TestContext.CancellationToken));
            }
        }
    }

    [TestMethod]
    public async Task GameAudio_MusicIsServedWithIntroSkippingStartOffset()
    {
        // Prüft die gelieferte MP3 und den Startpunkt 0:15 ohne Wiedergabe des Intros.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/game-audio", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
        var music = catalog.RootElement.GetProperty("music");
        Assert.AreEqual("music/bgm.mp3", music.GetProperty("file").GetString());
        Assert.AreEqual(15, music.GetProperty("startSeconds").GetInt32());
        Assert.IsInRange(0.15, 0.17, music.GetProperty("volume").GetDouble());
        using var audio = await client.GetAsync("/Assets/audio/music/bgm.mp3", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, audio.StatusCode);
        Assert.AreEqual("audio/mpeg", audio.Content.Headers.ContentType?.MediaType);
        Assert.IsNotEmpty(await audio.Content.ReadAsByteArrayAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GameAudio_ExposesSixPoolsAndExistingAudioWithoutFullManifest()
    {
        // Prüft die schmale Audio-Konfiguration, alle tatsächlichen Dateireferenzen und den Schutz der vollständigen Konfiguration.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/game-audio", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        using var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
        var pools = catalog.RootElement.GetProperty("pools");
        Assert.AreEqual(6, pools.GetArrayLength());
        var total = 0;
        foreach (var pool in pools.EnumerateArray())
        foreach (var variant in pool.GetProperty("variants").EnumerateArray())
        {
            using var audio = await client.GetAsync("/Assets/audio/" + variant.GetString(), TestContext.CancellationToken);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, audio.StatusCode);
            Assert.StartsWith("audio/", audio.Content.Headers.ContentType!.MediaType!);
            total++;
        }
        Assert.AreEqual(28, total);
        using var fullManifest = await client.GetAsync("/Assets/config/manifest.json", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, fullManifest.StatusCode);
    }

    [TestMethod]
    public async Task GameAudio_MissingClipIsNotServedAsSuccess()
    {
        // Prüft fehlende Audiodateien ohne irreführende erfolgreiche Antwort.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/Assets/audio/laser/missing_01.wav", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task ActionResolved_WireOutcomeUsesCanonicalSnakeCaseForShaftHit()
    {
        // Prüft den tatsächlichen SignalR-JSON-Vertrag statt einer durch Enum-Konverter verdeckten Zeichenkettenabweichung.
        await using var factory = CreateFactory();
        await using var connection = CreateConnection(factory);
        var notification = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("ActionResolved", action => notification.TrySetResult(action));
        await connection.StartAsync(TestContext.CancellationToken);
        await connection.InvokeAsync<JoinResponse>("JoinGame", new JoinRequest("wire-player", ClientRole.Player), TestContext.CancellationToken);
        Assert.IsTrue(await connection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken));
        var engine = factory.Services.GetRequiredService<VGame.Domain.GameEngine>();
        Assert.IsTrue(engine.RegisterTool(CreateTool("wire-shaft")));
        var request = new ActionRequested("wire-player", 1, engine.GetSnapshot().RoundId!.Value, "wire-shaft", DateTimeOffset.UtcNow,
            new GesturePayload(GestureKind.Tap, [new Point2(0.3, 0.5)], 0, 50));
        await connection.InvokeAsync<ActionCommandResult>("SubmitAction", request, TestContext.CancellationToken);
        var action = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.AreEqual("shaft_hit", action.GetProperty("outcome").GetString());
    }

#if DEBUG
    [TestMethod]
    [DataRow("tool_0002")]
    [DataRow("tool_0006")]
    [DataRow("tool_0010")]
    public async Task ArtworkCatalog_CoversEveryOpaquePixelOfLoadedGameImage(string assetId)
    {
        // Prüft Bild und tatsächlich geladenen Spielkatalog gemeinsam auf vollständige Silhouettenabdeckung.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var definition = Assert.ContainsSingle(factory.Services.GetRequiredService<IReadOnlyList<ToolDefinition>>().Where(tool => tool.AssetId == assetId));
        using var bitmap = SKBitmap.Decode(await client.GetByteArrayAsync($"/Assets/images/tools/{assetId}.webp", TestContext.CancellationToken));
        Assert.IsNotNull(bitmap);
        var zones = new AssetZoneAuthoring { AssetId = assetId, Canvas = new() { Width = bitmap.Width, Height = bitmap.Height }, Pivot = new(0.5, 0.5),
            HitZones = definition.HitZones.Select(zone => new ManifestHitZone { ZoneId = zone.ZoneId, Role = zone.Role == HitZoneRole.Cut ? "cut" : "shaft",
                Points = zone.Points.Select(point => new[] { point.X, point.Y }).ToArray() }).ToArray() };
        var metadata = new AssetMetadata { AssetId = assetId, Category = "tool-image", LicenseStatus = "test", Source = "test-fixture", CreatedUtc = DateTimeOffset.UnixEpoch };
        AssetValidator.ValidateZones(JsonSerializer.SerializeToElement(zones, ManifestInputs.JsonOptions), metadata, bitmap);
    }
#endif

    [TestMethod]
    [DataRow("/play")]
    [DataRow("/watcher")]
    public async Task ClientPages_DisableCachingAcrossPublishedUpdates(string requestPath)
    {
        // Prüft den Cache-Schutz der HTML-Einstiegspunkte, damit neue Paketstände keine alten Bundle-Verweise übernehmen.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(requestPath, TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
    }

    [TestMethod]
    public async Task WorkshopEvents_ReturnsOnlyValidatedVisitorConfiguration()
    {
        // Prüft den öffentlichen Konfigurationsabruf ohne Freigabe der vollständigen Spielkonfiguration.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/workshop-events", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        var options = await response.Content.ReadFromJsonAsync<WorkshopEventOptions>(TestContext.CancellationToken);
        Assert.IsNotNull(options);
        options.Validate();
        Assert.HasCount(8, options.Events);
        Assert.AreEqual(1400, options.Events["gauge"].DurationMilliseconds);
        Assert.AreEqual(3000, options.Events["gauge"].CooldownMilliseconds);
        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.DoesNotContain("roundDurationSeconds", json);
        Assert.DoesNotContain("artwork", json);
    }

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(2000, 60000)]
    public void WorkshopEvents_ValidBoundarySettingsAreAccepted(int durationMilliseconds, int cooldownMilliseconds)
    {
        // Prüft die zulässigen Grenzwerte auch bei global deaktivierten Spielereien.
        var options = new WorkshopEventOptions { Enabled = false };
        options.Events["gauge"] = new() { Enabled = false, DurationMilliseconds = durationMilliseconds, CooldownMilliseconds = cooldownMilliseconds };
        options.Validate();
        Assert.IsFalse(options.Enabled);
        Assert.IsFalse(options.Events["gauge"].Enabled);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WorkshopEvents_ConfigurationChangesAreReturnedOnNextRequest(bool enabled)
    {
        // Prüft geänderte Freigaben und Zeiten beim nächsten Abruf ohne Neustart des Hosts.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        configuration["WorkshopEvents:Enabled"] = enabled.ToString();
        configuration["WorkshopEvents:Events:gauge:Enabled"] = "false";
        configuration["WorkshopEvents:Events:gauge:DurationMilliseconds"] = "2000";
        configuration["WorkshopEvents:Events:gauge:CooldownMilliseconds"] = "10000";

        var options = await client.GetFromJsonAsync<WorkshopEventOptions>("/api/workshop-events", TestContext.CancellationToken);
        Assert.IsNotNull(options);
        Assert.AreEqual(enabled, options.Enabled);
        Assert.IsFalse(options.Events["gauge"].Enabled);
        Assert.AreEqual(2000, options.Events["gauge"].DurationMilliseconds);
        Assert.AreEqual(10000, options.Events["gauge"].CooldownMilliseconds);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("2001")]
    [DataRow("invalid")]
    public async Task WorkshopEvents_InvalidRuntimeConfigurationReturnsServiceUnavailable(string durationMilliseconds)
    {
        // Prüft eine sichere Fehlerantwort bei ungültigen Laufzeitwerten ohne Auslieferung von Konfigurationsdetails.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        factory.Services.GetRequiredService<IConfiguration>()["WorkshopEvents:Events:gauge:DurationMilliseconds"] = durationMilliseconds;
        using var response = await client.GetAsync("/api/workshop-events", TestContext.CancellationToken);
        Assert.AreEqual(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.DoesNotContain("DurationMilliseconds", problem);
        Assert.DoesNotContain("ArgumentException", problem);
        Assert.DoesNotContain("InvalidOperationException", problem);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(2001, 0)]
    [DataRow(1400, -1)]
    [DataRow(1400, 60001)]
    public void WorkshopEvents_InvalidTimingIsRejected(int durationMilliseconds, int cooldownMilliseconds)
    {
        // Prüft die Ablehnung ungültiger Effektdauern und Cooldowns.
        var options = new WorkshopEventOptions();
        options.Events["gauge"] = new() { DurationMilliseconds = durationMilliseconds, CooldownMilliseconds = cooldownMilliseconds };
        Assert.ThrowsExactly<ArgumentException>(options.Validate);
    }

    [TestMethod]
    public void WorkshopEvents_UnknownMissingAndNullProfilesAreRejected()
    {
        // Prüft unbekannte, fehlende und leere Ereignisprofile statt stiller Ersatzwerte.
        var options = new WorkshopEventOptions();
        options.Events.Remove("gauge");
        Assert.ThrowsExactly<ArgumentException>(options.Validate);
        options.Events["unknown"] = new();
        Assert.ThrowsExactly<ArgumentException>(options.Validate);
        options.Events.Remove("unknown");
        options.Events["gauge"] = null!;
        Assert.ThrowsExactly<ArgumentException>(options.Validate);
        Assert.ThrowsExactly<ArgumentException>(new WorkshopEventOptions { Events = null! }.Validate);
    }

    [TestMethod]
    [DataRow("background/bg_0001.webp")]
    [DataRow("tools/tool_0002.webp")]
    [DataRow("tools/tool_0006.webp")]
    [DataRow("tools/tool_0010.webp")]
    public async Task ArtworkImages_AreServedFromCanonicalAssets(string imagePath)
    {
        // Prüft die Auslieferung der vier ausgewählten Laufzeitbilder aus dem zentralen Assets-Verzeichnis.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/Assets/images/{imagePath}", TestContext.CancellationToken);

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.IsNotEmpty(await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ArtworkImages_DoNotExposeConfigurationOrMissingImages()
    {
        // Prüft, dass nur Bilder freigegeben werden und fehlende Bilder keinen erfolgreichen Abruf ergeben.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var requestPath in new[] { "/Assets/config/gameplay.json", "/Assets/images/tools/missing.webp" })
        {
            using var response = await client.GetAsync(requestPath, TestContext.CancellationToken);
            Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [TestMethod]
    [DataRow("tool_0002", 0.90)]
    [DataRow("tool_0006", 0.80)]
    [DataRow("tool_0010", 0.90)]
    public void ArtworkCatalog_ClassifiesCutShaftAndTransparentMargin(string assetId, double cuttingPointX)
    {
        // Prüft die Prototypzonen der freigestellten Bilder einschließlich Schaftpriorität und transparentem Rand.
        using var factory = CreateFactory();
        var catalog = factory.Services.GetRequiredService<IReadOnlyList<ToolDefinition>>();
        var definition = Assert.ContainsSingle(catalog.Where(tool => tool.AssetId == assetId));
        var tools = new[] { new ToolSnapshot(definition, ToolState.Dull) };

        var cuttingContact = VGame.Domain.HitZoneClassifier.FindFirstContact(tools, [new Point2(cuttingPointX, 0.5)], 0);
        var shaftContact = VGame.Domain.HitZoneClassifier.FindFirstContact(tools, [new Point2(0.5, 0.5)], 0);
        var marginContact = VGame.Domain.HitZoneClassifier.FindFirstContact(tools, [new Point2(0.01, 0.01)], 0);

        Assert.IsNotNull(cuttingContact);
        Assert.AreEqual(HitZoneRole.Cut, cuttingContact.HitZone.Role);
        Assert.IsNotNull(shaftContact);
        Assert.AreEqual(HitZoneRole.Shaft, shaftContact.HitZone.Role);
        Assert.IsNull(marginContact);
        Assert.IsFalse(definition.HasPkd);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task JoinGame_WatcherReceivesSnapshotAndCannotStartRound()
    {
        // Prüft einen echten SignalR-Beitritt, den Snapshot-Broadcast und die passive Watcherrolle.
        await using var factory = CreateFactory();
        await using var firstConnection = CreateConnection(factory);
        var firstSnapshotNotification = new TaskCompletionSource<GameSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        firstConnection.On<GameSnapshot>("SnapshotUpdated", snapshot => firstSnapshotNotification.TrySetResult(snapshot));
        await firstConnection.StartAsync(TestContext.CancellationToken);

        var firstJoin = await firstConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("first-watcher", ClientRole.Watcher),
            TestContext.CancellationToken);
        var firstNotification = await firstSnapshotNotification.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        await using var secondConnection = CreateConnection(factory);
        var laterFirstNotification = new TaskCompletionSource<GameSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        firstConnection.On<GameSnapshot>("SnapshotUpdated", snapshot =>
        {
            if (snapshot.StateVersion > firstNotification.StateVersion)
            {
                laterFirstNotification.TrySetResult(snapshot);
            }
        });
        await secondConnection.StartAsync(TestContext.CancellationToken);
        var secondJoin = await secondConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("second-watcher", ClientRole.Watcher),
            TestContext.CancellationToken);

        var broadcastSnapshot = await laterFirstNotification.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        var startAccepted = await firstConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken);

        Assert.AreEqual(ClientRole.Watcher, firstJoin.Role);
        Assert.AreEqual(ClientRole.Watcher, secondJoin.Role);
        Assert.AreEqual(GamePhase.Attract, firstJoin.Snapshot.Phase);
        Assert.IsGreaterThan(firstNotification.StateVersion, broadcastSnapshot.StateVersion);
        Assert.IsFalse(startAccepted);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task JoinGame_SecondLocalPlayerTakesOverIdleRole()
    {
        // Prüft den Rollen-Handoff zweier lokaler /play-Ansichten und blockiert den vorherigen Tab.
        await using var factory = CreateFactory();
        await using var previousPlayerConnection = CreateConnection(factory);
        await previousPlayerConnection.StartAsync(TestContext.CancellationToken);
        var previousPlayerJoin = await previousPlayerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("previous-player", ClientRole.Player),
            TestContext.CancellationToken);
        var handoffSnapshot = new TaskCompletionSource<GameSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        previousPlayerConnection.On<GameSnapshot>("SnapshotUpdated", snapshot =>
        {
            if (snapshot.ActiveClientId == "current-player")
            {
                handoffSnapshot.TrySetResult(snapshot);
            }
        });

        await using var currentPlayerConnection = CreateConnection(factory);
        await currentPlayerConnection.StartAsync(TestContext.CancellationToken);
        var currentPlayerJoin = await currentPlayerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("current-player", ClientRole.Player),
            TestContext.CancellationToken);
        var receivedHandoff = await handoffSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        var previousStartAccepted = await previousPlayerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken);
        var currentStartAccepted = await currentPlayerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken);

        Assert.AreEqual(ClientRole.Player, previousPlayerJoin.Role);
        Assert.AreEqual(ClientRole.Player, currentPlayerJoin.Role);
        Assert.AreEqual("current-player", receivedHandoff.ActiveClientId);
        Assert.IsFalse(previousStartAccepted);
        Assert.IsTrue(currentStartAccepted);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task RequestPlayerRole_DuringRoundWaitsUntilRoundIsReleased()
    {
        // Prüft, dass eine zweite lokale Play-Ansicht während der Runde wartet und danach per Hub übernimmt.
        await using var factory = CreateFactory();
        await using var currentPlayerConnection = CreateConnection(factory);
        await currentPlayerConnection.StartAsync(TestContext.CancellationToken);
        await currentPlayerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("current-player", ClientRole.Player),
            TestContext.CancellationToken);
        Assert.IsTrue(await currentPlayerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken));

        await using var waitingPlayerConnection = CreateConnection(factory);
        await waitingPlayerConnection.StartAsync(TestContext.CancellationToken);
        var waitingJoin = await waitingPlayerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("waiting-player", ClientRole.Player),
            TestContext.CancellationToken);
        var takeoverDuringRound = await waitingPlayerConnection.InvokeAsync<bool>("RequestPlayerRole", TestContext.CancellationToken);

        Assert.AreEqual(ClientRole.Watcher, waitingJoin.Role);
        Assert.IsFalse(takeoverDuringRound);
        Assert.IsTrue(await currentPlayerConnection.InvokeAsync<bool>("AbortRoundAndReleaseRole", true, TestContext.CancellationToken));
        Assert.IsTrue(await waitingPlayerConnection.InvokeAsync<bool>("RequestPlayerRole", TestContext.CancellationToken));
        var snapshot = await waitingPlayerConnection.InvokeAsync<GameSnapshot>("GetSnapshot", TestContext.CancellationToken);

        Assert.AreEqual("waiting-player", snapshot.ActiveClientId);
        Assert.AreEqual(GamePhase.Attract, snapshot.Phase);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task AbortRoundAndReleaseRole_RequiresConfirmationAndAllowsHandoff()
    {
        // Prüft lokalen Slide-to-confirm-Abbruch, Snapshotzustellung und anschließende Rollenübergabe.
        await using var factory = CreateFactory();
        await using var playerConnection = CreateConnection(factory);
        await playerConnection.StartAsync(TestContext.CancellationToken);
        var playerJoin = await playerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("player", ClientRole.Player),
            TestContext.CancellationToken);
        Assert.AreEqual(ClientRole.Player, playerJoin.Role);
        Assert.IsTrue(await playerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken));

        var unconfirmedAbort = await playerConnection.InvokeAsync<bool>("AbortRoundAndReleaseRole", false, TestContext.CancellationToken);
        var confirmedAbort = await playerConnection.InvokeAsync<bool>("AbortRoundAndReleaseRole", true, TestContext.CancellationToken);
        var abortedSnapshot = await playerConnection.InvokeAsync<GameSnapshot>("GetSnapshot", TestContext.CancellationToken);

        await using var nextPlayerConnection = CreateConnection(factory);
        await nextPlayerConnection.StartAsync(TestContext.CancellationToken);
        var nextPlayerJoin = await nextPlayerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("next-player", ClientRole.Player),
            TestContext.CancellationToken);

        Assert.IsFalse(unconfirmedAbort);
        Assert.IsTrue(confirmedAbort);
        Assert.AreEqual(GamePhase.Attract, abortedSnapshot.Phase);
        Assert.IsNull(abortedSnapshot.ActiveClientId);
        Assert.AreEqual(ClientRole.Player, nextPlayerJoin.Role);
        Assert.IsGreaterThan(playerJoin.RoleVersion, nextPlayerJoin.RoleVersion);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task SubmitAction_DuplicateSequence_BroadcastsAndScoresOnlyOnce()
    {
        // Prüft eine bestätigte Spieleraktion über SignalR und die Idempotenz wiederholter Sequenzen.
        await using var factory = CreateFactory();
        await using var playerConnection = CreateConnection(factory);
        var resolvedEvents = new ConcurrentQueue<ActionResolved>();
        var firstResolvedEvent = new TaskCompletionSource<ActionResolved>(TaskCreationOptions.RunContinuationsAsynchronously);
        playerConnection.On<ActionResolved>("ActionResolved", action =>
        {
            resolvedEvents.Enqueue(action);
            firstResolvedEvent.TrySetResult(action);
        });
        await playerConnection.StartAsync(TestContext.CancellationToken);
        await playerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("player", ClientRole.Player),
            TestContext.CancellationToken);
        Assert.IsTrue(await playerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken));
        var snapshot = await playerConnection.InvokeAsync<GameSnapshot>("GetSnapshot", TestContext.CancellationToken);
        var gameEngine = factory.Services.GetRequiredService<VGame.Domain.GameEngine>();
        Assert.IsTrue(gameEngine.RegisterTool(CreateTool("tool-1")));
        var actionRequest = CreateTapRequest(snapshot.RoundId!.Value, 1);

        var firstResult = await playerConnection.InvokeAsync<ActionCommandResult>(
            "SubmitAction",
            actionRequest,
            TestContext.CancellationToken);
        var firstBroadcast = await firstResolvedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        var duplicateResult = await playerConnection.InvokeAsync<ActionCommandResult>(
            "SubmitAction",
            actionRequest,
            TestContext.CancellationToken);

        Assert.IsTrue(firstResult.Accepted);
        Assert.IsFalse(firstResult.Duplicate);
        Assert.AreEqual(Outcome.Success, firstResult.Resolution!.Outcome);
        Assert.IsTrue(duplicateResult.Duplicate);
        Assert.AreEqual(firstBroadcast.EventId, duplicateResult.Resolution!.EventId);
        Assert.AreEqual(10, gameEngine.GetSnapshot().Score);
        Assert.HasCount(1, resolvedEvents);
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task StartRound_SpawnsPrototypeToolsInsideStageArea()
    {
        // Prüft, dass der serverseitige Prototyp-Spawner Ziele innerhalb der freien StageArea broadcastet.
        await using var factory = CreateFactory();
        await using var playerConnection = CreateConnection(factory);
        var spawnedTools = new TaskCompletionSource<GameSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        playerConnection.On<GameSnapshot>("SnapshotUpdated", snapshot =>
        {
            if (snapshot.Tools.Count > 0)
            {
                spawnedTools.TrySetResult(snapshot);
            }
        });
        await playerConnection.StartAsync(TestContext.CancellationToken);
        await playerConnection.InvokeAsync<JoinResponse>(
            "JoinGame",
            new JoinRequest("player", ClientRole.Player),
            TestContext.CancellationToken);
        Assert.IsTrue(await playerConnection.InvokeAsync<bool>("StartRound", TestContext.CancellationToken));

        var snapshot = await spawnedTools.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        var options = factory.Services.GetRequiredService<VGame.Domain.GameplayOptions>();

        Assert.IsLessThanOrEqualTo(options.MaxConcurrentTargets, snapshot.Tools.Count);
        Assert.IsNotEmpty(snapshot.Tools);
        Assert.IsTrue(snapshot.Tools.All(tool => tool.Definition.ToolId.StartsWith("prototype_", StringComparison.Ordinal)));
        Assert.IsTrue(snapshot.Tools.All(tool => tool.Placement is not null
            && tool.Placement.Center.X >= options.StageAreaX + tool.Definition.BoundingWidthNormalized / 2
            && tool.Placement.Center.X <= options.StageAreaX + options.StageAreaWidth - tool.Definition.BoundingWidthNormalized / 2
            && tool.Placement.Center.Y >= options.StageAreaY + tool.Definition.BoundingHeightNormalized / 2
            && tool.Placement.Center.Y <= options.StageAreaY + options.StageAreaHeight - tool.Definition.BoundingHeightNormalized / 2));
        var firstTool = snapshot.Tools[0];
        var leftEntryX = options.StageAreaX + firstTool.Definition.BoundingWidthNormalized / 2;
        var rightEntryX = options.StageAreaX + options.StageAreaWidth - firstTool.Definition.BoundingWidthNormalized / 2;
        var edgeDistance = Math.Min(
            Math.Abs(firstTool.Placement!.Center.X - leftEntryX),
            Math.Abs(firstTool.Placement.Center.X - rightEntryX));
        Assert.IsLessThanOrEqualTo(0.02, edgeDistance);
    }

    private static HubConnection CreateConnection(WebApplicationFactory<Program> factory)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/gameHub"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new OutcomeJsonConverter());
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            })
            .Build();
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClientOriginPolicy>();
                services.AddSingleton<IClientOriginPolicy, TestLocalClientOriginPolicy>();
            });
        });
    }

    private sealed class TestLocalClientOriginPolicy : IClientOriginPolicy
    {
        public bool IsLocal(HttpContext? httpContext) => true;
    }

    private static ToolDefinition CreateTool(string toolId)
    {
        return new ToolDefinition(
            toolId,
            0,
            [
                CreateRectangle("shaft", HitZoneRole.Shaft, 0.2, 0.2, 0.5, 0.8),
                CreateRectangle("cut", HitZoneRole.Cut, 0.5, 0.2, 0.8, 0.8)
            ],
            0,
            0.6,
            0.6);
    }

    private static HitZoneDefinition CreateRectangle(string zoneId, HitZoneRole role, double left, double top, double right, double bottom)
    {
        return new HitZoneDefinition(zoneId, role,
        [
            new Point2(left, top),
            new Point2(right, top),
            new Point2(right, bottom),
            new Point2(left, bottom)
        ]);
    }

    private static ActionRequested CreateTapRequest(Guid roundId, long sequence)
    {
        return new ActionRequested(
            "player",
            sequence,
            roundId,
            "tool-1",
            DateTimeOffset.UnixEpoch,
            new GesturePayload(GestureKind.Tap, [new Point2(0.65, 0.5)], 0, 0));
    }
}