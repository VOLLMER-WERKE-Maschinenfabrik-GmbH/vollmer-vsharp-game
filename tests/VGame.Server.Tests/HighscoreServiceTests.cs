using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Services;

namespace VGame.Server.Tests;

[TestClass]
[TestCategory("Server")]
public sealed class HighscoreServiceTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vgame-highscores-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public void Highscores_CompletedRoundPersistsWithoutExposingEmail()
    {
        // Prüft dauerhafte Speicherung, Wiederladen und den öffentlichen Vertrag ohne E-Mail oder Löschcode.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.IsTrue(service.StartRound("player", Registration()));
        var roundId = engine.GetSnapshot().RoundId!.Value;
        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();
        service.ProcessRound();
        service.ProcessRound();
        var entry = Assert.ContainsSingle(service.GetHighscores(GameMode.Laser));
        Assert.AreEqual("Test player", entry.DisplayName);
        var publicJson = JsonSerializer.Serialize(entry);
        Assert.DoesNotContain("email", publicJson.ToLowerInvariant());
        Assert.DoesNotContain("deletion", publicJson.ToLowerInvariant());
        var storedJson = File.ReadAllText(Path.Combine(_directory, "highscores.json")).ToLowerInvariant();
        Assert.DoesNotContain("email", storedJson);
        Assert.DoesNotContain("leaderboardconsent", storedJson);
        Assert.DoesNotContain("privacyversion", storedJson);
        Assert.IsEmpty(service.GetHighscores(GameMode.Saw));
        Assert.IsNotNull(service.GetOwnHighscore("player", roundId));
        Assert.IsNull(service.GetOwnHighscore("watcher", roundId));
        var reloaded = CreateService(engine, clock);
        Assert.AreEqual(entry.EntryId, Assert.ContainsSingle(reloaded.GetHighscores(GameMode.Laser)).EntryId);
    }

    [TestMethod]
    public void Highscores_LegacyEmailDataIsRemovedWhenLoaded()
    {
        // Prüft, dass beim Laden älterer Highscore-Dateien E-Mail- und Einwilligungsdaten sofort entfernt werden.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var storagePath = Path.Combine(_directory, "highscores.json");
        Directory.CreateDirectory(_directory);
        var timestamp = clock.GetUtcNow();
        var legacyEntry = new[]
        {
            new
            {
                roundId = Guid.NewGuid(),
                ownerClientId = "player",
                entry = new
                {
                    entryId = Guid.NewGuid(),
                    displayName = "Test player",
                    mode = GameMode.Laser,
                    score = 25,
                    completedAt = timestamp
                },
                email = "old@example.invalid",
                leaderboardConsent = true,
                prizeNotificationConsent = true,
                privacyVersion = "2026-10-27",
                consentedAt = timestamp,
                expiresAt = timestamp.AddHours(12),
                deletionCode = new string('A', 48)
            }
        };
        File.WriteAllText(storagePath, JsonSerializer.Serialize(legacyEntry));

        var service = CreateService(engine, clock, storagePath);

        Assert.HasCount(1, service.GetHighscores(GameMode.Laser));
        var storedJson = File.ReadAllText(storagePath).ToLowerInvariant();
        Assert.DoesNotContain("old@example.invalid", storedJson);
        Assert.DoesNotContain("email", storedJson);
        Assert.DoesNotContain("consent", storedJson);
        Assert.DoesNotContain("privacyversion", storedJson);
    }

    [TestMethod]
    public void Highscores_UsesModeSelectedAtRoundStartAfterModeChangesBeforePersistence()
    {
        // Prüft, dass ein Moduswechsel nach Rundenende die Ranglistenzuordnung nicht verändert.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.IsTrue(engine.SelectMode("player", GameMode.Laser));
        Assert.IsTrue(service.StartRound("player", Registration()));

        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();
        Assert.AreEqual(GamePhase.Results, engine.GetSnapshot().Phase);
        Assert.IsTrue(engine.SelectMode("player", GameMode.Saw));

        service.ProcessRound();

        Assert.HasCount(1, service.GetHighscores(GameMode.Laser));
        Assert.IsEmpty(service.GetHighscores(GameMode.Saw));
    }

    [TestMethod]
    public void Highscores_AbortedAndAnonymousRoundsAreNotSaved()
    {
        // Prüft, dass Besucherabbrüche und Runden ohne Registrierung keine personenbezogenen Einträge erzeugen.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.IsTrue(service.StartRound("player", Registration()));
        Assert.IsTrue(engine.AbortRound("player", engine.GetSnapshot().RoundId!.Value));
        service.ProcessRound();
        Assert.IsEmpty(service.GetHighscores(GameMode.Laser));
        Assert.IsTrue(service.StartRound("player"));
        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();
        service.ProcessRound();
        Assert.IsEmpty(service.GetHighscores(GameMode.Laser));
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "highscores.json")));
    }

    [TestMethod]
    public void Highscores_ExpiredEntriesAreRemovedAtNextLocalMidnight()
    {
        // Prüft die vereinbarte Löschung am folgenden deutschen Kalendertag einschließlich Neustart.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.IsTrue(service.StartRound("player", Registration()));
        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();
        Assert.HasCount(1, service.GetHighscores(GameMode.Laser));
        clock.Advance(TimeSpan.FromHours(11));
        Assert.HasCount(1, service.GetHighscores(GameMode.Laser));
        clock.Advance(TimeSpan.FromHours(1));
        service.ProcessRound();
        Assert.IsEmpty(CreateService(engine, clock).GetHighscores(GameMode.Laser));
    }

    [TestMethod]
    public void Highscores_StorageRetryPreservesCompletionTimestampAndExpiry()
    {
        // Prüft, dass ein fehlgeschlagener Speicherversuch Abschlusszeit und folgende Ortsmitternacht beibehält.
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 10, 28, 22, 58, 50, TimeSpan.Zero));
        var engine = CreateEngine(clock);
        var storagePath = Path.Combine(_directory, "highscores.json");
        Directory.CreateDirectory(storagePath);
        var service = CreateService(engine, clock, storagePath);
        Assert.IsTrue(service.StartRound("player", Registration()));
        clock.Advance(TimeSpan.FromSeconds(60));
        var completedAt = clock.GetUtcNow();
        engine.AdvanceClock();

        Assert.ThrowsExactly<UnauthorizedAccessException>(service.ProcessRound);
        clock.Advance(TimeSpan.FromSeconds(5));
        Directory.Delete(storagePath);
        service.ProcessRound();

        var entry = Assert.ContainsSingle(service.GetHighscores(GameMode.Laser));
        Assert.AreEqual(completedAt, entry.CompletedAt);
        using var document = JsonDocument.Parse(File.ReadAllText(storagePath));
        var expiresAt = document.RootElement[0].GetProperty("expiresAt").GetDateTimeOffset();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var nextLocalMidnight = TimeZoneInfo.ConvertTime(completedAt, timeZone).Date.AddDays(1);
        var expectedExpiry = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, timeZone));
        Assert.AreEqual(expectedExpiry, expiresAt);
    }

    [TestMethod]
    public void Highscores_RetryAfterPendingExpiryDiscardsRegistration()
    {
        // Prüft, dass ein Speicherversuch nach der zugesagten Löschfrist keinen neuen Eintrag mehr erzeugt.
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 10, 28, 22, 58, 50, TimeSpan.Zero));
        var engine = CreateEngine(clock);
        var storagePath = Path.Combine(_directory, "highscores.json");
        Directory.CreateDirectory(storagePath);
        var service = CreateService(engine, clock, storagePath);
        Assert.IsTrue(service.StartRound("player", Registration()));
        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();

        Assert.ThrowsExactly<UnauthorizedAccessException>(service.ProcessRound);
        clock.Advance(TimeSpan.FromSeconds(11));
        Directory.Delete(storagePath);
        service.ProcessRound();

        Assert.IsEmpty(service.GetHighscores(GameMode.Laser));
        Assert.IsFalse(File.Exists(storagePath));
    }

    [TestMethod]
    public void Highscores_DeletionCodeRemovesTheEntirePrivateRecord()
    {
        // Prüft die Löschung über den privaten Code und die Ablehnung unbekannter Codes.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.IsTrue(service.StartRound("player", Registration()));
        var roundId = engine.GetSnapshot().RoundId!.Value;
        clock.Advance(TimeSpan.FromSeconds(60));
        engine.AdvanceClock();
        var own = service.GetOwnHighscore("player", roundId);
        Assert.IsNotNull(own);
        Assert.IsFalse(service.DeleteHighscore("invalid"));
        Assert.IsTrue(service.DeleteHighscore(own.DeletionCode));
        Assert.IsFalse(service.DeleteHighscore(own.DeletionCode));
        Assert.IsEmpty(CreateService(engine, clock).GetHighscores(GameMode.Laser));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" 	 ")]
    [DataRow("This display name is longer than forty chars")]
    [DataRow("\u0001")]
    public void Highscores_InvalidNameDoesNotStartRound(string name)
    {
        // Prüft, dass leere, zu lange oder Steuerzeichen enthaltende Namen serverseitig abgewiesen werden.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        Assert.ThrowsExactly<ArgumentException>(() => service.StartRound("player", new PlayerRegistration(name)));
        Assert.AreEqual(GamePhase.Attract, engine.GetSnapshot().Phase);
    }

    [TestMethod]
    public void Highscores_NameOnlyRegistrationAndImmediateRestartAreSupported()
    {
        // Prüft den Start nur mit Namen, die Ablehnung eines Watchers und Speicherung vor einem unmittelbaren Neustart.
        var clock = new AdjustableTimeProvider();
        var engine = CreateEngine(clock);
        var service = CreateService(engine, clock);
        var registration = new PlayerRegistration("Test player");
        Assert.IsFalse(service.StartRound("watcher", registration));
        Assert.IsTrue(service.StartRound("player", registration));
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.IsTrue(service.StartRound("player"));
        Assert.HasCount(1, service.GetHighscores(GameMode.Laser));
    }

    private static PlayerRegistration Registration() => new("Test player");

    private static GameEngine CreateEngine(TimeProvider clock)
    {
        var engine = new GameEngine(new GameplayOptions(), clock);
        engine.JoinClient(new JoinRequest("player", ClientRole.Player), true);
        engine.JoinClient(new JoinRequest("watcher", ClientRole.Watcher), true);
        return engine;
    }

    private HighscoreService CreateService(GameEngine engine, TimeProvider clock, string? storagePath = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlayerData:StoragePath"] = storagePath ?? Path.Combine(_directory, "highscores.json")
        }).Build();
        return new HighscoreService(engine, clock, configuration);
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public AdjustableTimeProvider(DateTimeOffset? now = null) => _now = now ?? new DateTimeOffset(2026, 10, 27, 11, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public override long GetTimestamp() => _now.UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}