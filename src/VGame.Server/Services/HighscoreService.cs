using System.Security.Cryptography;
using System.Text.Json;
using VGame.Contracts;
using VGame.Domain;

namespace VGame.Server.Services;

public sealed class HighscoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();
    private readonly GameEngine _gameEngine;
    private readonly TimeProvider _timeProvider;
    private readonly string _filePath;
    private readonly TimeZoneInfo _timeZone;
    private readonly List<StoredHighscore> _entries;
    private PendingRegistration? _pending;

    public HighscoreService(GameEngine gameEngine, TimeProvider timeProvider, IConfiguration configuration)
    {
        _gameEngine = gameEngine;
        _timeProvider = timeProvider;
        _filePath = configuration["PlayerData:StoragePath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vollmer", "VGame", "highscores.json");
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration["PlayerData:TimeZoneId"] ?? "Europe/Berlin");
        if (File.Exists(_filePath))
        {
            var storedData = File.ReadAllText(_filePath);
            _entries = JsonSerializer.Deserialize<List<StoredHighscore>>(storedData, JsonOptions)
                ?? throw new InvalidDataException("Highscore-Datei ist ungültig.");
            using var document = JsonDocument.Parse(storedData);
            var hasLegacyFields = document.RootElement.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.Object)
                .SelectMany(entry => entry.EnumerateObject())
                .Any(property => property.Name.Equals("Email", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("LeaderboardConsent", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("PrizeNotificationConsent", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("PrivacyVersion", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("ConsentedAt", StringComparison.OrdinalIgnoreCase));
            if (hasLegacyFields) Persist(_entries);
        }
        else _entries = [];
    }

    public bool StartRound(string clientInstanceId, PlayerRegistration? registration = null)
    {
        var validated = registration is null ? null : Validate(registration);
        lock (_sync)
        {
            if (_gameEngine.GetSnapshot().ActiveClientId != clientInstanceId
                || _gameEngine.GetClientRole(clientInstanceId) != ClientRole.Player) return false;
            _gameEngine.AdvanceClock();
            ProcessCurrentRound();
            if (!_gameEngine.StartRound(clientInstanceId)) return false;
            var startedSnapshot = _gameEngine.GetSnapshot();
            _pending = validated is null ? null : new PendingRegistration(
                startedSnapshot.RoundId!.Value, clientInstanceId, validated, startedSnapshot.Mode);
            return true;
        }
    }

    public void ProcessRound()
    {
        lock (_sync) ProcessCurrentRound();
    }

    public IReadOnlyList<HighscoreEntry> GetHighscores(GameMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Ungültiger Spielmodus.");
        lock (_sync)
        {
            ProcessCurrentRound();
            return _entries.Where(entry => entry.Entry.Mode == mode).Select(entry => entry.Entry)
                .OrderByDescending(entry => entry.Score).ThenBy(entry => entry.CompletedAt).Take(20).ToArray();
        }
    }

    public OwnHighscore? GetOwnHighscore(string clientInstanceId, Guid roundId)
    {
        lock (_sync)
        {
            if (_gameEngine.GetSnapshot().ActiveClientId != clientInstanceId
                || _gameEngine.GetClientRole(clientInstanceId) != ClientRole.Player) return null;
            ProcessCurrentRound();
            var stored = _entries.SingleOrDefault(entry => entry.RoundId == roundId && entry.OwnerClientId == clientInstanceId);
            return stored is null ? null : new OwnHighscore(stored.Entry, stored.DeletionCode);
        }
    }

    public bool DeleteHighscore(string deletionCode)
    {
        if (deletionCode is null || deletionCode.Length != 48) return false;
        lock (_sync)
        {
            var entry = _entries.SingleOrDefault(candidate => candidate.DeletionCode == deletionCode);
            if (entry is null) return false;
            var remaining = _entries.Where(candidate => candidate != entry).ToList();
            Persist(remaining);
            _entries.Remove(entry);
            return true;
        }
    }

    private PlayerRegistration Validate(PlayerRegistration registration)
    {
        var name = registration.DisplayName?.Trim().Normalize() ?? "";
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl))
            throw new ArgumentException("Bitte einen Namen oder Fantasienamen mit 1 bis 40 Zeichen eingeben.");
        return registration with { DisplayName = name };
    }

    private void ProcessCurrentRound()
    {
        var now = _timeProvider.GetUtcNow();
        var retained = _entries.Where(entry => entry.ExpiresAt > now).ToList();
        if (retained.Count != _entries.Count)
        {
            Persist(retained);
            _entries.RemoveAll(entry => entry.ExpiresAt <= now);
        }
        if (_pending is null) return;
        var snapshot = _gameEngine.GetSnapshot();
        if (snapshot.RoundId != _pending.RoundId || snapshot.WasAborted)
        {
            _pending = null;
            return;
        }
        if (snapshot.Phase != GamePhase.Results) return;
        var pending = _pending;
        var completedAt = pending.CompletedAt ?? now;
        var expiresAt = pending.ExpiresAt ?? GetNextLocalMidnight(completedAt);
        pending = pending with { CompletedAt = completedAt, ExpiresAt = expiresAt };
        _pending = pending;
        if (expiresAt <= now)
        {
            _pending = null;
            return;
        }

        var entry = new StoredHighscore(pending.RoundId, pending.ClientInstanceId,
            new HighscoreEntry(Guid.NewGuid(), pending.Registration.DisplayName, pending.Mode, snapshot.Score, completedAt),
            expiresAt, Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
        Persist([.. _entries, entry]);
        _entries.Add(entry);
        _pending = null;
    }

    private DateTimeOffset GetNextLocalMidnight(DateTimeOffset completedAt)
    {
        var localDate = TimeZoneInfo.ConvertTime(completedAt, _timeZone).Date.AddDays(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDate, _timeZone));
    }

    private void Persist(IReadOnlyList<StoredHighscore> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        var temporaryPath = _filePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record PendingRegistration(Guid RoundId, string ClientInstanceId, PlayerRegistration Registration,
        GameMode Mode, DateTimeOffset? CompletedAt = null, DateTimeOffset? ExpiresAt = null);

    private sealed record StoredHighscore(Guid RoundId, string OwnerClientId, HighscoreEntry Entry,
        DateTimeOffset ExpiresAt, string DeletionCode);
}