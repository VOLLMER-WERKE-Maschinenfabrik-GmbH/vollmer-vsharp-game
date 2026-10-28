namespace VGame.Contracts;

public enum GameMode
{
    Laser,
    Saw
}

public enum GamePhase
{
    Attract,
    Playing,
    Results
}

public enum ClientRole
{
    Watcher,
    Player
}

public enum GestureKind
{
    Tap,
    Swipe
}

public enum HitZoneRole
{
    Cut,
    Shaft
}

public enum Outcome
{
    Success,
    WrongSharpening,
    ShaftHit,
    Miss
}

public enum ToolState
{
    Dull,
    InProgress,
    Sharp,
    Failed
}

public sealed record Point2(double X, double Y);

public sealed record GesturePayload(
    GestureKind Kind,
    IReadOnlyList<Point2> Points,
    long StartedAtMs,
    long EndedAtMs);

public sealed record HitZoneDefinition(
    string ZoneId,
    HitZoneRole Role,
    IReadOnlyList<Point2> Points);

public sealed record ToolDefinition(
    string ToolId,
    int ZOrder,
    IReadOnlyList<HitZoneDefinition> HitZones,
    double CutDirectionDegrees,
    double BoundingWidthNormalized,
    double BoundingHeightNormalized,
    string VisualKind = "drill",
    bool HasPkd = false,
    string? AssetId = null);

public sealed record ToolPlacement(
    Point2 Center,
    double VelocityXPerSecond,
    double VelocityYPerSecond,
    double RotationDegrees,
    CubicBezierPath? Curve = null,
    double CurveTimeSeconds = 0,
    double AngularVelocityDegreesPerSecond = 0);

public sealed record CubicBezierPath(Point2 Start, Point2 FirstControl, Point2 SecondControl, Point2 End, double DurationSeconds);

public sealed record ToolSnapshot(ToolDefinition Definition, ToolState State, ToolPlacement? Placement = null);

public sealed record JoinRequest(
    string ClientInstanceId,
    ClientRole RequestedRole,
    string? ResumeToken = null);

public sealed record JoinResponse(
    string ClientInstanceId,
    ClientRole Role,
    long RoleVersion,
    string ResumeToken,
    GameSnapshot Snapshot);

public sealed record ActionRequested(
    string ClientInstanceId,
    long CommandSequence,
    Guid RoundId,
    string ToolId,
    DateTimeOffset InputTimestamp,
    GesturePayload Gesture);

public sealed record ResolvedHitZone(string ZoneId, HitZoneRole Role);

public sealed record ActionResolved(
    Guid RoundId,
    Guid EventId,
    long StateVersion,
    Outcome Outcome,
    string? ToolId,
    ResolvedHitZone? HitZone,
    string AudioEvent,
    int ScoreDelta,
    int Score,
    int Combo,
    ToolState? ToolState,
    DateTimeOffset ServerTimestamp,
    Point2? FeedbackPoint = null);

public sealed record ActionCommandResult(
    bool Accepted,
    bool Duplicate,
    ActionResolved? Resolution,
    string? RejectionReason);

public sealed record GameSnapshot(
    Guid? RoundId,
    long StateVersion,
    GamePhase Phase,
    string? ActiveClientId,
    GameMode Mode,
    long RemainingMs,
    int Score,
    int Combo,
    long RoleVersion,
    IReadOnlyList<ToolSnapshot> Tools,
    GameStageArea? StageArea = null,
    bool WasAborted = false);

public sealed record PlayerRegistration(string DisplayName, string? Email, bool LeaderboardConsent,
    bool PrizeNotificationConsent, string PrivacyVersion);

public sealed record HighscoreEntry(Guid EntryId, string DisplayName, GameMode Mode, int Score, DateTimeOffset CompletedAt);

public sealed record PlayerDataNotice(string Version, string ContactEmail, string TimeZoneId, string Retention);

public sealed record OwnHighscore(HighscoreEntry Entry, string DeletionCode);

public sealed record GameStageArea(double Left, double Top, double Width, double Height);

public sealed record DemoSnapshot(string Presentation, long RemainingMs, GameSnapshot? Game,
    IReadOnlyList<ActionResolved> Actions);