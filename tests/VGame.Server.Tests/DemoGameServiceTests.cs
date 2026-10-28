using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VGame.Server.Tests;

[TestClass]
[TestCategory("Server")]
public sealed class DemoGameServiceTests
{
    [TestMethod]
    public void Demo_AlternatesLogoLaserLogoSawAtExactMinuteBoundaries()
    {
        // Prüft den vollständigen Vier-Minuten-Ablauf mit exakt einminütigen Präsentationsabschnitten.
        var clock = new DemoTimeProvider();
        var realGame = new GameEngine(new GameplayOptions(), clock);
        var service = new DemoGameService(realGame, new GameplayOptions(), clock, Tools());
        Assert.AreEqual("logo", service.Advance().Presentation);
        clock.Advance(TimeSpan.FromMilliseconds(59_999));
        Assert.AreEqual("logo", service.Advance().Presentation);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var laser = service.Advance();
        Assert.AreEqual("laser", laser.Presentation);
        Assert.AreEqual(60_000, laser.RemainingMs);
        Assert.AreEqual(GameMode.Laser, laser.Game!.Mode);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual("logo", service.Advance().Presentation);
        clock.Advance(TimeSpan.FromSeconds(60));
        var saw = service.Advance();
        Assert.AreEqual("saw", saw.Presentation);
        Assert.AreEqual(GameMode.Saw, saw.Game!.Mode);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual("logo", service.Advance().Presentation);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual("laser", service.Advance().Presentation);
    }

    [TestMethod]
    [DataRow(GameMode.Laser)]
    [DataRow(GameMode.Saw)]
    public void Demo_StartsWithMinimumConcurrentTargets(GameMode mode)
    {
        // Prüft, dass der Demo-Modus bei beiden Werkzeugarten direkt mehrere Ziele vorführt.
        var clock = new DemoTimeProvider();
        var options = new GameplayOptions { StageAreaX = 0, StageAreaY = 0, StageAreaWidth = 1, StageAreaHeight = 1 };
        var service = new DemoGameService(new GameEngine(options, clock), options, clock, Tools());
        service.Advance();
        clock.Advance(TimeSpan.FromSeconds(mode == GameMode.Laser ? 60 : 180));

        var demo = service.Advance();

        Assert.AreEqual(mode, demo.Game!.Mode);
        Assert.IsGreaterThanOrEqualTo(options.MinimumConcurrentTargets, demo.Game.Tools.Count);
    }

    [TestMethod]
    [DataRow(GameMode.Laser)]
    [DataRow(GameMode.Saw)]
    public void Demo_ComputerUsesRealActionsWithSuccessesAndMistakesWithoutChangingRealGame(GameMode mode)
    {
        // Prüft echte Erfolgs-/Fehlerausgänge im isolierten Motor und unveränderte öffentliche Spielerdaten.
        var clock = new DemoTimeProvider();
        var options = new GameplayOptions { StageAreaX = 0, StageAreaY = 0, StageAreaWidth = 1, StageAreaHeight = 1 };
        var realGame = new GameEngine(options, clock);
        realGame.JoinClient(new JoinRequest("visitor", ClientRole.Player), true);
        var before = realGame.GetSnapshot();
        var service = new DemoGameService(realGame, options, clock, Tools());
        service.Advance();
        clock.Advance(TimeSpan.FromSeconds(mode == GameMode.Laser ? 60 : 180));
        var outcomes = new List<Outcome>();
        var moved = false;
        var previousCenters = new Dictionary<string, Point2>();
        for (var tick = 0; tick < 590; tick++)
        {
            var snapshot = service.Advance();
            Assert.AreEqual(mode, snapshot.Game!.Mode);
            outcomes.AddRange(snapshot.Actions.Select(action => action.Outcome));
            foreach (var tool in snapshot.Game.Tools)
            {
                if (previousCenters.TryGetValue(tool.Definition.ToolId, out var previous) && previous != tool.Placement!.Center) moved = true;
                previousCenters[tool.Definition.ToolId] = tool.Placement!.Center;
            }
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }
        Assert.Contains(Outcome.Success, outcomes);
        Assert.Contains(Outcome.Miss, outcomes);
        Assert.IsTrue(outcomes.Any(outcome => outcome is Outcome.ShaftHit or Outcome.WrongSharpening));
        Assert.IsTrue(moved);
        var after = realGame.GetSnapshot();
        Assert.AreEqual(before.StateVersion, after.StateVersion);
        Assert.AreEqual(before.Phase, after.Phase);
        Assert.AreEqual(before.ActiveClientId, after.ActiveClientId);
        Assert.AreEqual(before.Score, after.Score);
        Assert.IsEmpty(after.Tools);
        Assert.IsNull(after.RoundId);
    }

    [TestMethod]
    [DataRow(GameMode.Laser)]
    [DataRow(GameMode.Saw)]
    public void Demo_ProductionArtworkProducesSuccessfulAndFailedComputerActions(GameMode mode)
    {
        // Prüft den Bot mit den tatsächlichen rotierenden Werkzeugkonturen statt vereinfachter Testrechtecke.
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        var artwork = JsonSerializer.Deserialize<ToolDefinition[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "config", "artwork.prototype.json")), jsonOptions)!;
        var clock = new DemoTimeProvider();
        var options = new GameplayOptions { StageAreaX = 0, StageAreaY = 0, StageAreaWidth = 1, StageAreaHeight = 1 };
        var service = new DemoGameService(new GameEngine(options, clock), options, clock, artwork);
        service.Advance();
        clock.Advance(TimeSpan.FromSeconds(mode == GameMode.Laser ? 60 : 180));
        var outcomes = new List<Outcome>();
        for (var tick = 0; tick < 590; tick++)
        {
            outcomes.AddRange(service.Advance().Actions.Select(action => action.Outcome));
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }
        Assert.Contains(Outcome.Success, outcomes);
        Assert.Contains(Outcome.Miss, outcomes);
        Assert.IsTrue(outcomes.Any(outcome => outcome is Outcome.ShaftHit or Outcome.WrongSharpening));
    }

    [TestMethod]
    public void Demo_RealRoundInterruptsAndRestartsWithFullLogoMinute()
    {
        // Prüft den Vorrang einer echten Besucherrunde und die neue Logo-Minute nach deren Abbruch.
        var clock = new DemoTimeProvider();
        var options = new GameplayOptions();
        var realGame = new GameEngine(options, clock);
        realGame.JoinClient(new JoinRequest("visitor", ClientRole.Player), true);
        var service = new DemoGameService(realGame, options, clock, Tools());
        service.Advance();
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual("laser", service.Advance().Presentation);
        Assert.IsTrue(realGame.StartRound("visitor"));
        Assert.AreEqual("inactive", service.Advance().Presentation);
        clock.Advance(TimeSpan.FromSeconds(15));
        Assert.AreEqual("inactive", service.Advance().Presentation);
        Assert.IsTrue(realGame.AbortRound("visitor", realGame.GetSnapshot().RoundId!.Value));
        var restarted = service.Advance();
        Assert.AreEqual("logo", restarted.Presentation);
        Assert.AreEqual(60_000, restarted.RemainingMs);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.AreEqual("laser", service.Advance().Presentation);
    }

    private static IReadOnlyList<ToolDefinition> Tools() => [Tool("drill", "drill"), Tool("saw", "saw")];

    private static ToolDefinition Tool(string identifier, string kind) => new(identifier, 0,
        [new HitZoneDefinition("shaft", HitZoneRole.Shaft, [new(0.1, 0.2), new(0.45, 0.2), new(0.45, 0.8), new(0.1, 0.8)]),
         new HitZoneDefinition("cut", HitZoneRole.Cut, [new(0.55, 0.2), new(0.9, 0.2), new(0.9, 0.8), new(0.55, 0.8)])],
        0, 0.16, 0.14, kind);

    private sealed class DemoTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public override long GetTimestamp() => _now.UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}