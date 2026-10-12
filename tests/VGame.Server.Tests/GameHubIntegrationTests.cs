using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using VGame.Contracts;
using VGame.Server.Security;

namespace VGame.Server.Tests;

[TestClass]
[TestCategory("Server")]
public sealed class GameHubIntegrationTests
{
    public TestContext TestContext { get; set; }

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
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                options.PayloadSerializerOptions.Converters.Add(new OutcomeJsonConverter());
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