using System.Collections.Concurrent;
using VGame.Contracts;
using VGame.Domain;

namespace VGame.Domain.Tests;

[TestClass]
[TestCategory("Domain")]
public sealed class GameEngineTests
{
    [TestMethod]
    public void RegisterTool_ArtworkAtRightStageEdge_AcceptsFloatingPointRounding()
    {
        // Prüft den Sägeblatt-Randeintritt, dessen Kantenrechnung einen minimalen Rundungsrest erzeugt.
        var options = new GameplayOptions();
        var engine = CreateEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch), options);
        JoinPlayer(engine);
        Assert.IsTrue(engine.StartRound("player"));
        var definition = CreateTool("artwork-saw") with
        {
            BoundingWidthNormalized = 0.12,
            BoundingHeightNormalized = 0.12 * 16 / 9 * 1024 / 1023
        };
        var placement = new ToolPlacement(
            new Point2(options.StageAreaX + options.StageAreaWidth - definition.BoundingWidthNormalized / 2, 0.6),
            -0.1, 0, 0);

        Assert.IsTrue(engine.RegisterTool(definition, placement));
    }

    [TestMethod]
    public void JoinClient_LocalPlayerRequest_TakesOverOnlyWhenIdle()
    {
        // Prüft die lokale Rollenübernahme im Leerlauf und den unveränderten Schutz vor Fernspielern.
        var engine = CreateEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var localPlayer = engine.JoinClient(new JoinRequest("local-player", ClientRole.Player), true);
        var secondLocalPlayer = engine.JoinClient(new JoinRequest("second-local", ClientRole.Player), true);
        var remotePlayer = engine.JoinClient(new JoinRequest("remote-player", ClientRole.Player), false);

        Assert.AreEqual(ClientRole.Player, localPlayer.Role);
        Assert.AreEqual(ClientRole.Player, secondLocalPlayer.Role);
        Assert.AreEqual(ClientRole.Watcher, remotePlayer.Role);
        Assert.AreEqual(ClientRole.Watcher, engine.GetClientRole("local-player"));
        Assert.AreEqual("second-local", engine.GetSnapshot().ActiveClientId);
        Assert.IsGreaterThan(localPlayer.RoleVersion, secondLocalPlayer.RoleVersion);
    }

    [TestMethod]
    public void JoinClient_ConcurrentLocalRequests_GrantOnePlayerRole()
    {
        // Prüft atomar, dass parallele lokale Beitritte keine zweite Spielerrolle vergeben.
        var engine = CreateEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var results = new ConcurrentBag<JoinResponse>();

        Parallel.For(0, 24, index =>
        {
            results.Add(engine.JoinClient(new JoinRequest($"client-{index}", ClientRole.Player), true));
        });

        var activeClients = results
            .Select(result => result.ClientInstanceId)
            .Where(clientId => engine.GetClientRole(clientId) == ClientRole.Player)
            .ToArray();
        Assert.ContainsSingle(activeClients);
        Assert.AreEqual(engine.GetSnapshot().ActiveClientId, activeClients[0]);
    }

    [TestMethod]
    public void JoinClient_LocalPlayerRequest_DoesNotTakeOverDuringRound()
    {
        // Prüft, dass eine neue lokale Ansicht den Spieler während einer laufenden Runde nicht verdrängt.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        var nextClient = engine.JoinClient(new JoinRequest("next-player", ClientRole.Player), true);

        Assert.AreEqual(ClientRole.Watcher, nextClient.Role);
        Assert.AreEqual("player", engine.GetSnapshot().ActiveClientId);
        Assert.AreEqual(GamePhase.Playing, engine.GetSnapshot().Phase);
    }

    [TestMethod]
    public void RequestPlayerRole_OnlyLocalWaitingPlayerCanClaimOutsideRound()
    {
        // Prüft lokale Claim-Ablehnung während einer Runde und Übernahme nach dem Rundenende.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        var waitingPlayer = engine.JoinClient(new JoinRequest("waiting-player", ClientRole.Player), true);
        var remoteClient = engine.JoinClient(new JoinRequest("remote-player", ClientRole.Player), false);
        var watcher = engine.JoinClient(new JoinRequest("watcher", ClientRole.Watcher), true);

        Assert.AreEqual(ClientRole.Watcher, waitingPlayer.Role);
        Assert.IsFalse(engine.RequestPlayerRole("waiting-player", isLocalClient: true));
        Assert.IsFalse(engine.RequestPlayerRole("remote-player", isLocalClient: true));
        Assert.IsFalse(engine.RequestPlayerRole("watcher", isLocalClient: true));

        timeProvider.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual(GamePhase.Results, engine.AdvanceClock()!.Phase);
        Assert.IsTrue(engine.RequestPlayerRole("waiting-player", isLocalClient: true));

        Assert.AreEqual("waiting-player", engine.GetSnapshot().ActiveClientId);
        Assert.AreEqual(ClientRole.Watcher, engine.GetClientRole("player"));
        Assert.AreEqual(ClientRole.Player, engine.GetClientRole("waiting-player"));
        Assert.AreEqual(ClientRole.Watcher, engine.GetClientRole("watcher"));
        Assert.AreEqual(ClientRole.Watcher, remoteClient.Role);
    }

    [TestMethod]
    public void StartRound_DefaultDuration_EndsAtSixtySeconds()
    {
        // Prüft den 60-Sekunden-Standard und das exakte serverseitige Rundenende.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateEngine(timeProvider);
        JoinPlayer(engine);
        Assert.IsTrue(engine.StartRound("player"));
        Assert.AreEqual(60_000, engine.GetSnapshot().RemainingMs);

        timeProvider.Advance(TimeSpan.FromSeconds(59));
        Assert.IsNull(engine.AdvanceClock());
        Assert.AreEqual(1_000, engine.GetSnapshot().RemainingMs);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var endedSnapshot = engine.AdvanceClock();

        Assert.IsNotNull(endedSnapshot);
        Assert.AreEqual(GamePhase.Results, endedSnapshot.Phase);
        Assert.AreEqual(0, endedSnapshot.RemainingMs);
    }

    [TestMethod]
    public void StartRound_ConfiguredDuration_UsesConfiguredBoundary()
    {
        // Prüft, dass die validierte Rundendauer ohne Codeänderung konfigurierbar ist.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateEngine(timeProvider, new GameplayOptions { RoundDurationSeconds = 30 });
        JoinPlayer(engine);
        Assert.IsTrue(engine.StartRound("player"));

        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var endedSnapshot = engine.AdvanceClock();

        Assert.IsNotNull(endedSnapshot);
        Assert.AreEqual(GamePhase.Results, endedSnapshot.Phase);
    }

    [TestMethod]
    public void ResolveAction_DuplicateCommand_AppliesScoreOnlyOnce()
    {
        // Prüft, dass ein wiederholter Befehl dasselbe Ergebnis liefert und nicht erneut punktet.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1")));
        var request = CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, new Point2(0.65, 0.5), timeProvider.GetUtcNow());

        var firstResult = engine.ResolveAction(request);
        var duplicateResult = engine.ResolveAction(request);

        Assert.IsTrue(firstResult.Accepted);
        Assert.IsFalse(firstResult.Duplicate);
        Assert.AreEqual(Outcome.Success, firstResult.Resolution!.Outcome);
        Assert.IsTrue(duplicateResult.Accepted);
        Assert.IsTrue(duplicateResult.Duplicate);
        Assert.AreEqual(firstResult.Resolution.EventId, duplicateResult.Resolution!.EventId);
        Assert.AreEqual(firstResult.Resolution.StateVersion, duplicateResult.Resolution.StateVersion);
        Assert.AreEqual(10, engine.GetSnapshot().Score);
        Assert.AreEqual(ToolState.Sharp, engine.GetSnapshot().Tools[0].State);
    }

    [TestMethod]
    public void ResolveAction_LaserShaftBoundary_PrefersShaftAndAllowsNegativeScore()
    {
        // Prüft die Schaftpriorität, den höheren Abzug und das Fortsetzen mit negativem Punktestand.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1")));
        var request = CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, new Point2(0.5, 0.5), timeProvider.GetUtcNow());

        var result = engine.ResolveAction(request);
        var snapshot = engine.GetSnapshot();

        Assert.AreEqual(Outcome.ShaftHit, result.Resolution!.Outcome);
        Assert.AreEqual(-10, snapshot.Score);
        Assert.AreEqual(GamePhase.Playing, snapshot.Phase);
        Assert.AreEqual(ToolState.Failed, snapshot.Tools[0].State);
        Assert.AreEqual("laser_fail", result.Resolution.AudioEvent);
    }

    [TestMethod]
    public void ResolveAction_SawSwipe_ValidDirectionAndLengthSucceeds()
    {
        // Prüft Richtung und Mindeststrecke relativ zur längsten Werkzeugkante im Säge-Modus.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateEngine(timeProvider);
        JoinPlayer(engine);
        Assert.IsTrue(engine.SelectMode("player", GameMode.Saw));
        Assert.IsTrue(engine.StartRound("player"));
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1")));
        var request = CreateSwipeRequest(
            "player",
            engine.GetSnapshot().RoundId!.Value,
            1,
            [new Point2(0.55, 0.5), new Point2(0.85, 0.5)],
            timeProvider.GetUtcNow());

        var result = engine.ResolveAction(request);

        Assert.AreEqual(Outcome.Success, result.Resolution!.Outcome);
        Assert.AreEqual("saw_success", result.Resolution.AudioEvent);
        Assert.AreEqual(10, engine.GetSnapshot().Score);
    }

    [TestMethod]
    public void ResolveAction_Miss_UsesPenaltyWithoutEndingRound()
    {
        // Prüft den Fehlschlagabzug ohne vorzeitiges Spielende.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        var request = CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, new Point2(0.05, 0.05), timeProvider.GetUtcNow());

        var result = engine.ResolveAction(request);
        var snapshot = engine.GetSnapshot();

        Assert.AreEqual(Outcome.Miss, result.Resolution!.Outcome);
        Assert.AreEqual(-5, snapshot.Score);
        Assert.AreEqual(GamePhase.Playing, snapshot.Phase);
        Assert.IsNull(result.Resolution.ToolId);
        Assert.IsNull(result.Resolution.HitZone);
    }

    [TestMethod]
    public void ResolveAction_WatcherOrStaleRound_IsRejected()
    {
        // Prüft Rollen- und Rundenvalidierung für nicht wertungsberechtigte Befehle.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        engine.JoinClient(new JoinRequest("watcher", ClientRole.Watcher), true);
        var currentRoundId = engine.GetSnapshot().RoundId!.Value;
        var watcherRequest = CreateTapRequest("watcher", currentRoundId, 1, new Point2(0.65, 0.5), timeProvider.GetUtcNow());
        var staleRoundRequest = CreateTapRequest("player", Guid.NewGuid(), 1, new Point2(0.65, 0.5), timeProvider.GetUtcNow());

        Assert.AreEqual("inactive_client", engine.ResolveAction(watcherRequest).RejectionReason);
        Assert.AreEqual("stale_round", engine.ResolveAction(staleRoundRequest).RejectionReason);
        Assert.AreEqual(0, engine.GetSnapshot().Score);
    }

    [TestMethod]
    public void ResolveAction_AfterRoundDeadline_IsRejected()
    {
        // Prüft, dass eine verspätete Geste nach Ablauf keine Punkte mehr verändern kann.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        timeProvider.Advance(TimeSpan.FromSeconds(60));
        var initialStateVersion = engine.GetSnapshot().StateVersion;
        var request = CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, new Point2(0.65, 0.5), timeProvider.GetUtcNow());

        var result = engine.ResolveAction(request);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("round_ended", result.RejectionReason);
        Assert.AreEqual(GamePhase.Results, engine.GetSnapshot().Phase);
        Assert.IsGreaterThan(initialStateVersion, engine.GetSnapshot().StateVersion);
        Assert.AreEqual(0, engine.GetSnapshot().Score);
    }

    [TestMethod]
    public void RegisterTool_AfterDeadline_LeavesTimeoutTransitionForClockService()
    {
        // Prüft, dass ein verspäteter Spawn das Rundenende nicht ohne Versionswechsel konsumiert.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        timeProvider.Advance(TimeSpan.FromSeconds(60));

        Assert.IsFalse(engine.RegisterTool(CreateTool("late-tool")));
        var endedSnapshot = engine.AdvanceClock();

        Assert.IsNotNull(endedSnapshot);
        Assert.AreEqual(GamePhase.Results, endedSnapshot.Phase);
    }

    [TestMethod]
    public void AdvanceClock_FailedTool_DespawnsAfterConfiguredDelay()
    {
        // Prüft das beschleunigte Despawn fehlbearbeiteter Werkzeuge nach 600 Millisekunden.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1")));
        var request = CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, new Point2(0.3, 0.5), timeProvider.GetUtcNow());
        Assert.AreEqual(Outcome.ShaftHit, engine.ResolveAction(request).Resolution!.Outcome);

        timeProvider.Advance(TimeSpan.FromMilliseconds(599));
        Assert.IsNull(engine.AdvanceClock());
        Assert.HasCount(1, engine.GetSnapshot().Tools);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        var snapshot = engine.AdvanceClock();

        Assert.IsNotNull(snapshot);
        Assert.IsEmpty(snapshot.Tools);
    }

    [TestMethod]
    public void AdvanceClock_MovesPlacedToolAndHitZonesWithIt()
    {
        // Prüft, dass serverseitige Zielbewegung und autoritative Trefferzonen dieselbe Platzierung verwenden.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        var placement = new ToolPlacement(new Point2(0.5, 0.6), 0.1, 0, 0);
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1"), placement));

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var movedSnapshot = engine.AdvanceClock();
        var movedTool = movedSnapshot!.Tools[0];
        var targetPoint = new Point2(
            movedTool.Placement!.Center.X + 0.15 * movedTool.Definition.BoundingWidthNormalized,
            movedTool.Placement.Center.Y);
        var request = CreateTapRequest("player", movedSnapshot.RoundId!.Value, 1, targetPoint, timeProvider.GetUtcNow());

        var result = engine.ResolveAction(request);

        Assert.AreEqual(0.6, movedTool.Placement.Center.X, 1e-9);
        Assert.AreEqual(Outcome.Success, result.Resolution!.Outcome);
        Assert.AreEqual(10, engine.GetSnapshot().Score);
    }

    [TestMethod]
    public void RegisterTool_PlacementOutsideStageArea_ThrowsArgumentException()
    {
        // Verhindert, dass ein serverseitiges Ziel teilweise außerhalb der validierten Bühne erscheint.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var placement = new ToolPlacement(new Point2(0.2, 0.6), 0, 0, 0);

        Assert.ThrowsExactly<ArgumentException>(() => engine.RegisterTool(CreateTool("tool-1"), placement));
    }

    [TestMethod]
    public void AdvanceClock_ApproachingTools_PreservesMinimumSpacing()
    {
        // Prüft, dass sich entgegengesetzt laufende Ziele nicht innerhalb der Mindestdistanz kreuzen.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        var leftDefinition = CreateSmallTool("left");
        var rightDefinition = CreateSmallTool("right");
        Assert.IsTrue(engine.RegisterTool(leftDefinition, new ToolPlacement(new Point2(0.43, 0.6), 0.1, 0, 0)));
        Assert.IsTrue(engine.RegisterTool(rightDefinition, new ToolPlacement(new Point2(0.65, 0.6), -0.1, 0, 0)));

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var snapshot = engine.AdvanceClock();
        var leftCenter = snapshot!.Tools.Single(tool => tool.Definition.ToolId == "left").Placement!.Center;
        var rightCenter = snapshot.Tools.Single(tool => tool.Definition.ToolId == "right").Placement!.Center;
        var distance = Math.Sqrt(Math.Pow(leftCenter.X - rightCenter.X, 2) + Math.Pow(leftCenter.Y - rightCenter.Y, 2));

        Assert.IsGreaterThanOrEqualTo(0.2, distance);
        Assert.IsInRange(0.13, 0.87, leftCenter.X);
        Assert.IsInRange(0.13, 0.87, rightCenter.X);
    }

    [TestMethod]
    public void DisconnectClient_ActivePlayer_AbortsRoundAndReleasesRole()
    {
        // Prüft, dass der Verbindungsverlust des aktiven Spielers keine verwaiste Runde hinterlässt.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        var snapshot = engine.DisconnectClient("player");

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(GamePhase.Attract, snapshot.Phase);
        Assert.IsNull(snapshot.ActiveClientId);
        Assert.IsNull(snapshot.RoundId);
        Assert.IsNull(engine.GetClientRole("player"));
    }

    [TestMethod]
    public void JoinClient_DisconnectedPlayerWithResumeToken_ReacquiresFreeRole()
    {
        // Prüft, dass ein authentischer lokaler Reconnect die freigegebene Spielerrolle übernehmen kann.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateEngine(timeProvider);
        var initialJoin = JoinPlayer(engine);
        Assert.IsTrue(engine.StartRound("player"));
        Assert.IsNotNull(engine.DisconnectClient("player"));

        var resumedJoin = engine.JoinClient(
            new JoinRequest("player", ClientRole.Player, initialJoin.ResumeToken),
            isLocalClient: true);

        Assert.AreEqual(ClientRole.Player, resumedJoin.Role);
        Assert.AreEqual("player", resumedJoin.Snapshot.ActiveClientId);
        Assert.IsGreaterThan(initialJoin.RoleVersion, resumedJoin.RoleVersion);
    }

    [TestMethod]
    public void ResolveAction_SawSwipe_WrongDirectionOrShortStrokeFails()
    {
        // Prüft Winkel- und Mindeststreckenfehler im Säge-Modus.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var wrongDirectionEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(wrongDirectionEngine.RegisterTool(CreateTool("tool-1", 90)));
        var wrongDirectionRequest = CreateSwipeRequest(
            "player",
            wrongDirectionEngine.GetSnapshot().RoundId!.Value,
            1,
            [new Point2(0.55, 0.5), new Point2(0.85, 0.5)],
            timeProvider.GetUtcNow());
        Assert.AreEqual(Outcome.WrongSharpening, wrongDirectionEngine.ResolveAction(wrongDirectionRequest).Resolution!.Outcome);

        var shortStrokeEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(shortStrokeEngine.RegisterTool(CreateTool("tool-1")));
        var shortStrokeRequest = CreateSwipeRequest(
            "player",
            shortStrokeEngine.GetSnapshot().RoundId!.Value,
            1,
            [new Point2(0.55, 0.5), new Point2(0.57, 0.5)],
            timeProvider.GetUtcNow());

        Assert.AreEqual(Outcome.WrongSharpening, shortStrokeEngine.ResolveAction(shortStrokeRequest).Resolution!.Outcome);
    }

    [TestMethod]
    public void AbortRoundAndReleaseRole_RequiresLocalConfirmation()
    {
        // Prüft, dass eine laufende Runde nur mit lokaler Bestätigung abgebrochen und freigegeben wird.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);

        Assert.IsFalse(engine.AbortRoundAndReleaseRole(isLocalOperator: false, confirmationComplete: true));
        Assert.IsFalse(engine.AbortRoundAndReleaseRole(isLocalOperator: true, confirmationComplete: false));
        Assert.IsTrue(engine.AbortRoundAndReleaseRole(isLocalOperator: true, confirmationComplete: true));
        Assert.AreEqual(GamePhase.Attract, engine.GetSnapshot().Phase);
        Assert.IsNull(engine.GetSnapshot().ActiveClientId);
    }

    [TestMethod]
    public void ReleaseRole_AfterRound_ClearsActivePlayerAndIncrementsRoleVersion()
    {
        // Prüft die bestätigte Rollenfreigabe nach regulärem Rundenende.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        timeProvider.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual(GamePhase.Results, engine.AdvanceClock()!.Phase);

        var initialRoleVersion = engine.GetSnapshot().RoleVersion;
        Assert.IsFalse(engine.ReleaseRole(isLocalOperator: true, confirmationComplete: false));
        Assert.IsTrue(engine.ReleaseRole(isLocalOperator: true, confirmationComplete: true));

        var snapshot = engine.GetSnapshot();
        Assert.IsNull(snapshot.ActiveClientId);
        Assert.IsGreaterThan(initialRoleVersion, snapshot.RoleVersion);
        Assert.AreEqual(ClientRole.Watcher, engine.GetClientRole("player"));
    }

    private static GameEngine CreateEngine(ManualTimeProvider timeProvider, GameplayOptions? options = null)
    {
        return new GameEngine(options ?? new GameplayOptions(), timeProvider);
    }

    private static GameEngine CreateRunningEngine(ManualTimeProvider timeProvider)
    {
        var engine = CreateEngine(timeProvider);
        JoinPlayer(engine);
        Assert.IsTrue(engine.StartRound("player"));
        return engine;
    }

    private static GameEngine CreateSawEngine(ManualTimeProvider timeProvider)
    {
        var engine = CreateEngine(timeProvider);
        JoinPlayer(engine);
        Assert.IsTrue(engine.SelectMode("player", GameMode.Saw));
        Assert.IsTrue(engine.StartRound("player"));
        return engine;
    }

    private static JoinResponse JoinPlayer(GameEngine engine)
    {
        return engine.JoinClient(new JoinRequest("player", ClientRole.Player), true);
    }

    private static ToolDefinition CreateTool(string toolId)
    {
        return CreateTool(toolId, 0);
    }

    private static ToolDefinition CreateTool(string toolId, double cutDirectionDegrees)
    {
        return new ToolDefinition(
            toolId,
            0,
            [
                CreateRectangle("shaft", HitZoneRole.Shaft, 0.2, 0.2, 0.5, 0.8),
                CreateRectangle("cut", HitZoneRole.Cut, 0.5, 0.2, 0.8, 0.8)
            ],
            cutDirectionDegrees,
            0.6,
            0.6);
    }

    private static ToolDefinition CreateSmallTool(string toolId)
    {
        return new ToolDefinition(
            toolId,
            0,
            [
                CreateRectangle("shaft", HitZoneRole.Shaft, 0.2, 0.2, 0.5, 0.8),
                CreateRectangle("cut", HitZoneRole.Cut, 0.5, 0.2, 0.8, 0.8)
            ],
            0,
            0.16,
            0.14);
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

    private static ActionRequested CreateTapRequest(string clientId, Guid roundId, long sequence, Point2 point, DateTimeOffset inputTimestamp)
    {
        return new ActionRequested(
            clientId,
            sequence,
            roundId,
            "tool-1",
            inputTimestamp,
            new GesturePayload(GestureKind.Tap, [point], 0, 0));
    }

    private static ActionRequested CreateSwipeRequest(string clientId, Guid roundId, long sequence, IReadOnlyList<Point2> points, DateTimeOffset inputTimestamp)
    {
        return new ActionRequested(
            clientId,
            sequence,
            roundId,
            "tool-1",
            inputTimestamp,
            new GesturePayload(GestureKind.Swipe, points, 0, 100));
    }
}