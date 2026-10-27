using System.Net.Mail;
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
    private readonly PlayerDataNotice _notice;
    private readonly List<StoredHighscore> _entries;
    private PendingRegistration? _pending;

    public HighscoreService(GameEngine gameEngine, TimeProvider timeProvider, IConfiguration configuration)
    {
        _gameEngine = gameEngine;
        _timeProvider = timeProvider;
        _filePath = configuration["PlayerData:StoragePath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vollmer", "VGame", "highscores.json");
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration["PlayerData:TimeZoneId"] ?? "Europe/Berlin");
        var contact = configuration["PlayerData:ContactEmail"] ?? "";
        if (!MailAddress.TryCreate(contact, out _)) throw new InvalidOperationException("Datenschutz-Kontakt fehlt.");
        _notice = new PlayerDataNotice(configuration["PlayerData:PrivacyVersion"] ?? "2026-10-27", contact,
            _timeZone.Id, "Löschung am folgenden Kalendertag um 00:00 Uhr in deutscher Ortszeit.");
        _entries = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<List<StoredHighscore>>(File.ReadAllText(_filePath), JsonOptions)
                ?? throw new InvalidDataException("Highscore-Datei ist ungültig.")
            : [];
    }

    public PlayerDataNotice GetNotice() => _notice;

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
            _pending = validated is null ? null : new PendingRegistration(
                _gameEngine.GetSnapshot().RoundId!.Value, clientInstanceId, validated, _timeProvider.GetUtcNow());
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
        if (!registration.LeaderboardConsent || registration.PrivacyVersion != _notice.Version)
            throw new ArgumentException("Bitte den aktuellen Datenschutzhinweis und die Ranglisteneinwilligung bestätigen.");
        var email = registration.Email?.Trim();
        if (registration.PrizeNotificationConsent)
        {
            if (email is null || email.Length > 254 || !MailAddress.TryCreate(email, out var address)
                || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase) || email.Any(char.IsControl))
                throw new ArgumentException("Für die Gewinnbenachrichtigung wird eine gültige E-Mail-Adresse benötigt.");
        }
        else if (!string.IsNullOrEmpty(email))
            throw new ArgumentException("E-Mail-Adressen werden nur mit Zustimmung zur Gewinnbenachrichtigung gespeichert.");
        return registration with { DisplayName = name, Email = registration.PrizeNotificationConsent ? email : null };
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
        var localDate = TimeZoneInfo.ConvertTime(now, _timeZone).Date.AddDays(1);
        var expiresAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDate, _timeZone));
        var entry = new StoredHighscore(_pending.RoundId, _pending.ClientInstanceId,
            new HighscoreEntry(Guid.NewGuid(), _pending.Registration.DisplayName, snapshot.Mode, snapshot.Score, now),
            _pending.Registration.Email, _pending.Registration.LeaderboardConsent, _pending.Registration.PrizeNotificationConsent,
            _pending.Registration.PrivacyVersion, _pending.ConsentedAt, expiresAt,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
        Persist([.. _entries, entry]);
        _entries.Add(entry);
        _pending = null;
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
        DateTimeOffset ConsentedAt);

    private sealed record StoredHighscore(Guid RoundId, string OwnerClientId, HighscoreEntry Entry, string? Email,
        bool LeaderboardConsent, bool PrizeNotificationConsent, string PrivacyVersion, DateTimeOffset ConsentedAt,
        DateTimeOffset ExpiresAt, string DeletionCode);
}