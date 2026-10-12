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
    double RotationDegrees);

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
    DateTimeOffset ServerTimestamp);

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
    IReadOnlyList<ToolSnapshot> Tools);