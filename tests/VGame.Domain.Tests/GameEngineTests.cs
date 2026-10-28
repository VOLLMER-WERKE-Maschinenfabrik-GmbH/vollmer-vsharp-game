using System.Collections.Concurrent;
using VGame.Contracts;
using VGame.Domain;

namespace VGame.Domain.Tests;

[TestClass]
[TestCategory("Domain")]
public sealed class GameEngineTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void FlyThrough_CrossesCanvasFromOutsideAndDespawns(int direction)
    {
        // Prüft Einflug von allen vier Seiten, gleichmäßigen Durchflug und Entfernen ohne Punkteabzug.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var start = direction switch { 0 => new Point2(-0.2, 0.6), 1 => new Point2(1.2, 0.6), 2 => new Point2(0.5, -0.3), _ => new Point2(0.5, 1.3) };
        var end = direction switch { 0 => new Point2(1.2, 0.6), 1 => new Point2(-0.2, 0.6), 2 => new Point2(0.5, 1.3), _ => new Point2(0.5, -0.3) };
        var curve = new CubicBezierPath(start,
            new(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3),
            new(start.X + (end.X - start.X) * 2 / 3, start.Y + (end.Y - start.Y) * 2 / 3), end, 4);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("fly-through"), new(start, 0, 0, 0, curve, AngularVelocityDegreesPerSecond: -24)));
        time.Advance(TimeSpan.FromSeconds(2));
        var middle = engine.AdvanceClock()!.Tools[0].Placement!;
        Assert.AreEqual(0.5, middle.Center.X, 1e-9);
        Assert.AreEqual(direction < 2 ? 0.6 : 0.5, middle.Center.Y, 1e-9);
        Assert.AreEqual(312d, middle.RotationDegrees, 1e-9);
        time.Advance(TimeSpan.FromSeconds(1.9));
        Assert.HasCount(1, engine.AdvanceClock()!.Tools);
        time.Advance(TimeSpan.FromSeconds(0.1));
        var finished = engine.AdvanceClock()!;
        Assert.IsEmpty(finished.Tools);
        Assert.AreEqual(0, finished.Score);
    }

    [TestMethod]
    public void FlyThrough_OverlappingTargetsContinueWithoutReversal()
    {
        // Prüft identische Startplätze und echte Überlagerung, ohne Umkehr oder Ausweichen zu erzwingen.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var curve = new CubicBezierPath(new(0.2, 0.6), new(0.4, 0.6), new(0.6, 0.6), new(0.8, 0.6), 4);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("first"), new(curve.Start, 0, 0, 0, curve)));
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("second"), new(curve.Start, 0, 0, 0, curve)));
        time.Advance(TimeSpan.FromSeconds(2));
        var tools = engine.AdvanceClock()!.Tools;
        Assert.HasCount(2, tools);
        foreach (var tool in tools) Assert.AreEqual(0.5, tool.Placement!.Center.X, 1e-9);
        time.Advance(TimeSpan.FromSeconds(1));
        foreach (var tool in engine.AdvanceClock()!.Tools) Assert.IsGreaterThan(0.5, tool.Placement!.Center.X);
    }

    [TestMethod]
    [DataRow(24, 350, 14, true)]
    [DataRow(-24, 10, 346, true)]
    [DataRow(24, 350, 14, false)]
    [DataRow(-24, 10, 346, false)]
    [DataRow(0, 30, 30, true)]
    public void Rotation_AdvancesInBothDirectionsAndWraps(double speed, double initial, double expected, bool curved)
    {
        // Prüft unabhängige Rechts-/Linksdrehung, Winkelüberlauf und den kompatiblen stillstehenden Standard.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var curve = new CubicBezierPath(new(0.25, 0.6), new(0.4, 0.7), new(0.6, 0.5), new(0.75, 0.6), 4);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("rotation"), new(curve.Start, 0, 0, initial, curved ? curve : null, AngularVelocityDegreesPerSecond: speed)));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(expected, engine.AdvanceClock()!.Tools[0].Placement!.RotationDegrees, 1e-9);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(181)]
    [DataRow(-181)]
    public void Rotation_InvalidAngularSpeedIsRejected(double speed)
    {
        // Prüft ungültige oder überhöhte Winkelgeschwindigkeiten vor der Registrierung.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        Assert.ThrowsExactly<ArgumentException>(() => engine.RegisterTool(CreateSmallTool("rotation"),
            new(new(0.5, 0.6), 0, 0, 0, AngularVelocityDegreesPerSecond: speed)));
    }

    [TestMethod]
    public void Rotation_LinearPlacementRequiresClearanceButFlightMayLeaveStage()
    {
        // Prüft weiterhin sichere lineare Platzierungen, während Flugkurven den bisherigen Bühnenrand überschreiten dürfen.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var definition = CreateSmallTool("rotation");
        Assert.ThrowsExactly<ArgumentException>(() => engine.RegisterTool(definition,
            new(new(0.13, 0.6), 0, 0, 0, AngularVelocityDegreesPerSecond: 24)));
        var curve = new CubicBezierPath(new(0.25, 0.6), new(0.4, 0.36), new(0.6, 0.6), new(0.75, 0.6), 4);
        Assert.IsTrue(engine.RegisterTool(definition,
            new(curve.Start, 0, 0, 0, curve, AngularVelocityDegreesPerSecond: -24)));
    }

    [TestMethod]
    public void Rotation_OverlappingCurveKeepsFlyingAndTurning()
    {
        // Prüft ungebremsten Flug und unveränderte Drehrichtung bei überlagerten Werkzeugen.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var curve = new CubicBezierPath(new(0.25, 0.6), new(0.4, 0.6), new(0.6, 0.6), new(0.75, 0.6), 4);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("rotation"), new(curve.Start, 0, 0, 0, curve, AngularVelocityDegreesPerSecond: -24)));
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("blocker"), new(new(0.5, 0.6), 0, 0, 0)));
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("vertical-overlap"), new(new(0.64, 0.76), 0, 0, 0, AngularVelocityDegreesPerSecond: 24)));
        time.Advance(TimeSpan.FromSeconds(2));
        var rotated = engine.AdvanceClock()!.Tools.Single(tool => tool.Definition.ToolId == "rotation").Placement!;
        Assert.AreEqual(0.5, rotated.Center.X, 1e-9);
        Assert.AreEqual(0.6, rotated.Center.Y, 1e-9);
        Assert.AreEqual(312d, rotated.RotationDegrees, 1e-9);
        Assert.AreEqual(-24d, rotated.AngularVelocityDegreesPerSecond);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(90, false)]
    [DataRow(90, true)]
    [DataRow(270, false)]
    public void Rotation_SawSwipeSucceedsWhenGrazingCutFromOutsideInEitherDirection(double degrees, bool reverse)
    {
        // Prüft kurze Streifzüge an gedrehten Scheiben in beiden Wischrichtungen.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateSawEngine(time);
        var definition = CreateSmallTool("tool-1");
        Assert.IsTrue(engine.RegisterTool(definition, new(new(0.5, 0.6), 0, 0, degrees)));
        var points = new[] { new Point2(0.65, 0.1), new Point2(0.65, 0.15) }
            .Select(point => TransformToolPoint(point, new(0.5, 0.6), 0.16, 0.14, degrees)).ToArray();
        if (reverse) Array.Reverse(points);
        var result = engine.ResolveAction(CreateSwipeRequest("player", engine.GetSnapshot().RoundId!.Value, 1, points, time.GetUtcNow()));
        Assert.AreEqual(Outcome.Success, result.Resolution!.Outcome);
    }

    [TestMethod]
    public void Bezier_CrossingCurvesPreserveAuthoritativeHitZones()
    {
        // Prüft echte Kurvenkreuzung und Trefferwertung an der tatsächlich bewegten Position.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var left = new CubicBezierPath(new(0.2, 0.6), new(0.35, 0.7), new(0.65, 0.5), new(0.8, 0.6), 6);
        var right = new CubicBezierPath(new(0.8, 0.6), new(0.65, 0.7), new(0.35, 0.5), new(0.2, 0.6), 6);
        var definition = CreateSmallTool("curve-left");
        Assert.IsTrue(engine.RegisterTool(definition, new(left.Start, 0, 0, 0, left)));
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("curve-right"), new(right.Start, 0, 0, 0, right)));
        var crossed = false;
        for (var step = 0; step < 40; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            var tools = engine.AdvanceClock()!.Tools;
            var first = tools[0].Placement!.Center;
            var second = tools[1].Placement!.Center;
            var distance = Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
            if (distance < 1e-9) crossed = true;
            foreach (var tool in tools)
            {
                Assert.IsInRange(0.05 + tool.Definition.BoundingWidthNormalized / 2 - 1e-9, 0.95 - tool.Definition.BoundingWidthNormalized / 2 + 1e-9, tool.Placement!.Center.X);
                Assert.IsInRange(0.28 + tool.Definition.BoundingHeightNormalized / 2 - 1e-9, 0.92 - tool.Definition.BoundingHeightNormalized / 2 + 1e-9, tool.Placement.Center.Y);
            }
        }
        Assert.IsTrue(crossed);
        var snapshot = engine.GetSnapshot();
        var target = snapshot.Tools[0];
        var point = new Point2(target.Placement!.Center.X + 0.15 * target.Definition.BoundingWidthNormalized, target.Placement.Center.Y);
        var result = engine.ResolveAction(CreateTapRequest("player", snapshot.RoundId!.Value, 1, point, time.GetUtcNow()));
        Assert.AreEqual(Outcome.Success, result.Resolution!.Outcome);
    }

    [TestMethod]
    public void Bezier_CurvesOnceAndStopsAtExactEndpoint()
    {
        // Prüft echte Krümmung und exakten Endpunkt ohne Rücklauf sowie das Entfernen abgeschlossener Ziele.
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(time);
        var curve = new CubicBezierPath(new(0.2, 0.5), new(0.35, 0.85), new(0.65, 0.85), new(0.8, 0.5), 4);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("curved"), new(curve.Start, 0, 0, 0, curve)));
        time.Advance(TimeSpan.FromSeconds(2));
        var middle = engine.AdvanceClock()!.Tools[0].Placement!;
        Assert.AreEqual(0.5, middle.Center.X, 1e-9);
        Assert.AreEqual(0.7625, middle.Center.Y, 1e-9);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.IsEmpty(engine.AdvanceClock()!.Tools);
        var end = BezierMotion.Advance(middle, 2);
        Assert.AreEqual(curve.End.X, end.Center.X, 1e-9);
        Assert.AreEqual(curve.End.Y, end.Center.Y, 1e-9);
        Assert.AreEqual(0, end.VelocityXPerSecond, 1e-9);
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.IsNull(engine.AdvanceClock());
        Assert.IsEmpty(engine.GetSnapshot().Tools);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    public void Bezier_InvalidControlDurationAndCenterAreRejected(int scenario)
    {
        // Prüft ungültige Kontrollpunkte, Dauer, Flugzeit und inkonsistenten Startzustand trotz erlaubter Außenpositionen.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var curve = new CubicBezierPath(new(0.2, 0.5), new(0.35, 0.75), new(0.65, 0.75), new(0.8, 0.5), 4);
        if (scenario == 0) curve = curve with { FirstControl = new(0.35, 4) };
        if (scenario == 1) curve = curve with { DurationSeconds = 0 };
        if (scenario == 3) curve = curve with { FirstControl = new(double.NaN, 0.5) };
        if (scenario == 4) curve = curve with { FirstControl = new(0.5, double.PositiveInfinity) };
        if (scenario == 7) curve = curve with { Start = null! };
        var center = scenario == 2 ? new Point2(0.5, 0.5) : curve.Start;
        var time = scenario == 5 ? -1 : scenario == 6 ? 5 : 0;
        Assert.ThrowsExactly<ArgumentException>(() => engine.RegisterTool(CreateSmallTool("curved"), new(center, 0, 0, 0, curve, time)));
    }

    [TestMethod]
    public void Bezier_SamplingAndLargeTimeStepsRemainDeterministic()
    {
        // Prüft taktratenunabhängige Kurvenpositionen und sichere große Zeitabstände.
        var curve = new CubicBezierPath(new(0.2, 0.5), new(0.35, 0.85), new(0.65, 0.85), new(0.8, 0.5), 4);
        var placement = new ToolPlacement(curve.Start, 0, 0, 0, curve);
        var sampled = placement;
        for (var index = 0; index < 200; index++) sampled = BezierMotion.Advance(sampled, 0.01);
        var direct = BezierMotion.Advance(placement, 2);
        Assert.AreEqual(direct.Center.X, sampled.Center.X, 1e-9);
        Assert.AreEqual(direct.Center.Y, sampled.Center.Y, 1e-9);
        var large = BezierMotion.Advance(placement, 802);
        Assert.AreEqual(curve.End.X, large.Center.X, 1e-9);
        Assert.AreEqual(curve.End.Y, large.Center.Y, 1e-9);
        Assert.AreEqual(curve.DurationSeconds, large.CurveTimeSeconds);
        var engineTime = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(engineTime);
        Assert.IsTrue(engine.RegisterTool(CreateSmallTool("large-step"), placement));
        engineTime.Advance(TimeSpan.FromSeconds(5));
        Assert.IsEmpty(engine.AdvanceClock()!.Tools);
    }

    [TestMethod]
    public void GetSnapshot_ContainsConfiguredAuthoritativeStageArea()
    {
        // Prüft gemeinsame Eingabe- und Spawn-Grenzen statt einer abweichenden hartcodierten Clientfläche.
        var options = new GameplayOptions { StageAreaX = 0.1, StageAreaY = 0.1, StageAreaWidth = 0.8, StageAreaHeight = 0.7 };
        var engine = CreateEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch), options);
        Assert.AreEqual(new GameStageArea(0.1, 0.1, 0.8, 0.7), engine.GetSnapshot().StageArea);
    }

    [TestMethod]
    public void ResolveAction_BroadcastsConfirmedFeedbackPointForPlayerAndWatcher()
    {
        // Prüft den bestätigten Tap-/Fehlpunkt im gemeinsamen Ereignis statt einer geratenen Watcher-Bildmitte.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        Assert.IsTrue(engine.RegisterTool(CreateTool("tool-1")));
        var point = new Point2(0.65, 0.5);
        var result = engine.ResolveAction(CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 1, point, timeProvider.GetUtcNow()));
        Assert.AreEqual(Outcome.Success, result.Resolution!.Outcome);
        Assert.AreEqual(point, result.Resolution.FeedbackPoint);
        var missPoint = new Point2(0.01, 0.01);
        var miss = engine.ResolveAction(CreateTapRequest("player", engine.GetSnapshot().RoundId!.Value, 2, missPoint, timeProvider.GetUtcNow()));
        Assert.AreEqual(Outcome.Miss, miss.Resolution!.Outcome);
        Assert.AreEqual(missPoint, miss.Resolution.FeedbackPoint);
    }

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
        Assert.IsFalse(endedSnapshot.WasAborted);
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
        // Prüft, dass ein kurzer Streifzug mit außerhalb liegenden Endpunkten zum Erfolg reicht.
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
            [new Point2(0.55, 0.19), new Point2(0.65, 0.19)],
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
    public void AdvanceClock_ApproachingTools_AllowOverlap()
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

        Assert.AreEqual(0.02, distance, 1e-9);
        Assert.AreEqual(0.53, leftCenter.X, 1e-9);
        Assert.AreEqual(0.55, rightCenter.X, 1e-9);
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
    public void ResolveAction_SawSwipe_IgnoresConfiguredDirectionAndStrokeLength()
    {
        // Prüft, dass Gegenrichtung und kurze Strecke keinen gültigen Schneidenstreifzug ablehnen.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var wrongDirectionEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(wrongDirectionEngine.RegisterTool(CreateTool("tool-1", 90)));
        var wrongDirectionRequest = CreateSwipeRequest(
            "player",
            wrongDirectionEngine.GetSnapshot().RoundId!.Value,
            1,
            [new Point2(0.55, 0.19), new Point2(0.65, 0.19)],
            timeProvider.GetUtcNow());
        Assert.AreEqual(Outcome.Success, wrongDirectionEngine.ResolveAction(wrongDirectionRequest).Resolution!.Outcome);

        var reverseEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(reverseEngine.RegisterTool(CreateTool("tool-1")));
        var reverseRequest = CreateSwipeRequest(
            "player",
            reverseEngine.GetSnapshot().RoundId!.Value,
            1,
            [new Point2(0.65, 0.19), new Point2(0.55, 0.19)],
            timeProvider.GetUtcNow());

        Assert.AreEqual(Outcome.Success, reverseEngine.ResolveAction(reverseRequest).Resolution!.Outcome);
    }

    [TestMethod]
    public void ResolveAction_SawSwipe_RequiresOutsideEndpointsAndFailsOnShaftContact()
    {
        // Prüft Außenstart/-ende und dass ein tiefer Treffer nach dem Schneidenkontakt das Werkzeug zerstört.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var insideStartEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(insideStartEngine.RegisterTool(CreateTool("tool-1")));
        var insideStartRequest = CreateSwipeRequest("player", insideStartEngine.GetSnapshot().RoundId!.Value, 1,
            [new Point2(0.55, 0.5), new Point2(0.85, 0.5)], timeProvider.GetUtcNow());
        Assert.AreEqual(Outcome.WrongSharpening, insideStartEngine.ResolveAction(insideStartRequest).Resolution!.Outcome);

        var insideEndEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(insideEndEngine.RegisterTool(CreateTool("tool-1")));
        var insideEndRequest = CreateSwipeRequest("player", insideEndEngine.GetSnapshot().RoundId!.Value, 1,
            [new Point2(0.15, 0.5), new Point2(0.55, 0.5)], timeProvider.GetUtcNow());
        Assert.AreEqual(Outcome.WrongSharpening, insideEndEngine.ResolveAction(insideEndRequest).Resolution!.Outcome);

        var shaftEngine = CreateSawEngine(timeProvider);
        Assert.IsTrue(shaftEngine.RegisterTool(CreateTool("tool-1")));
        var shaftRequest = CreateSwipeRequest("player", shaftEngine.GetSnapshot().RoundId!.Value, 1,
            [new Point2(0.85, 0.5), new Point2(0.15, 0.5)], timeProvider.GetUtcNow());
        var shaftResult = shaftEngine.ResolveAction(shaftRequest);

        Assert.AreEqual(Outcome.WrongSharpening, shaftResult.Resolution!.Outcome);
        Assert.AreEqual(ToolState.Failed, shaftResult.Resolution.ToolState);
        Assert.AreEqual(HitZoneRole.Shaft, shaftResult.Resolution.HitZone!.Role);
    }

    [TestMethod]
    public void AbortRound_ActivePlayer_EndsRoundAndPreservesScoreAndRole()
    {
        // Prüft den Besucherabbruch mit erhaltenem Punktestand und Spielerrolle sowie sofortigem Neustart.
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = CreateRunningEngine(timeProvider);
        Assert.IsTrue(engine.RegisterTool(CreateTool("abort-tool")));
        var before = engine.GetSnapshot();
        var request = CreateTapRequest("player", before.RoundId!.Value, 1, new Point2(0.65, 0.5), timeProvider.GetUtcNow());
        Assert.IsTrue(engine.ResolveAction(request).Accepted);
        var scored = engine.GetSnapshot();
        Assert.IsGreaterThan(0, scored.Score);

        Assert.IsTrue(engine.AbortRound("player", before.RoundId.Value));

        var aborted = engine.GetSnapshot();
        Assert.AreEqual(GamePhase.Results, aborted.Phase);
        Assert.IsTrue(aborted.WasAborted);
        Assert.AreEqual(0, aborted.RemainingMs);
        Assert.AreEqual(scored.Score, aborted.Score);
        Assert.AreEqual(scored.RoundId, aborted.RoundId);
        Assert.AreEqual(scored.RoleVersion, aborted.RoleVersion);
        Assert.AreEqual("player", aborted.ActiveClientId);
        Assert.AreEqual(ClientRole.Player, engine.GetClientRole("player"));
        Assert.IsEmpty(aborted.Tools);
        Assert.IsFalse(engine.ResolveAction(CreateTapRequest("player", before.RoundId.Value, 2,
            new Point2(0.65, 0.5), timeProvider.GetUtcNow())).Accepted);
        Assert.IsNull(engine.AdvanceClock());
        Assert.IsTrue(engine.StartRound("player"));
        Assert.AreNotEqual(before.RoundId, engine.GetSnapshot().RoundId);
        Assert.IsFalse(engine.GetSnapshot().WasAborted);
        Assert.AreEqual(0, engine.GetSnapshot().Score);
    }

    [TestMethod]
    [DataRow("watcher")]
    [DataRow("unknown")]
    public void AbortRound_NonActiveClientCannotEndRound(string clientInstanceId)
    {
        // Prüft, dass Zuschauer und unbekannte Clients keine laufende Besucherrunde abbrechen können.
        var engine = CreateRunningEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        engine.JoinClient(new JoinRequest("watcher", ClientRole.Watcher), true);
        var before = engine.GetSnapshot();

        Assert.IsFalse(engine.AbortRound(clientInstanceId, before.RoundId!.Value));

        Assert.AreEqual(GamePhase.Playing, engine.GetSnapshot().Phase);
        Assert.AreEqual(before.StateVersion, engine.GetSnapshot().StateVersion);
    }

    [TestMethod]
    public void AbortRound_NotPlayingOrStaleRound_IsRejected()
    {
        // Prüft ungültige und wiederholte Abbrüche sowie den Schutz einer neuen Runde vor verspäteten Befehlen.
        var engine = CreateEngine(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        JoinPlayer(engine);
        Assert.IsFalse(engine.AbortRound("player", Guid.NewGuid()));
        Assert.IsTrue(engine.StartRound("player"));
        var roundId = engine.GetSnapshot().RoundId!.Value;
        Assert.IsFalse(engine.AbortRound("player", Guid.NewGuid()));
        Assert.IsTrue(engine.AbortRound("player", roundId));
        var aborted = engine.GetSnapshot();
        Assert.IsFalse(engine.AbortRound("player", roundId));
        Assert.AreEqual(aborted.StateVersion, engine.GetSnapshot().StateVersion);
        Assert.IsTrue(engine.StartRound("player"));
        Assert.IsFalse(engine.AbortRound("player", roundId));
        Assert.AreEqual(GamePhase.Playing, engine.GetSnapshot().Phase);
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

    private static Point2 TransformToolPoint(Point2 point, Point2 center, double width, double height, double rotationDegrees)
    {
        var angle = rotationDegrees * Math.PI / 180;
        var localX = (point.X - 0.5) * width;
        var localY = (point.Y - 0.5) * height / ToolGeometry.CanvasAspectRatio;
        return new Point2(
            center.X + localX * Math.Cos(angle) - localY * Math.Sin(angle),
            center.Y + (localX * Math.Sin(angle) + localY * Math.Cos(angle)) * ToolGeometry.CanvasAspectRatio);
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