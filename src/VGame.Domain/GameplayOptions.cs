namespace VGame.Domain;

public sealed record GameplayOptions
{
    public int Version { get; init; } = 1;
    public int RoundDurationSeconds { get; init; } = 60;
    public int CorrectScore { get; init; } = 10;
    public int WrongSharpeningPenalty { get; init; } = -5;
    public int ShaftHitPenalty { get; init; } = -10;
    public int MissPenalty { get; init; } = -5;
    public double ContactToleranceNormalized { get; init; } = 0.02;
    public double MaxAngleDeviationDeg { get; init; } = 35;
    public double MinStrokeLengthNormalized { get; init; } = 0.18;
    public int FailedDespawnMs { get; init; } = 600;
    public int SharpDespawnMs { get; init; } = 1800;
    public int MaxConcurrentTargets { get; init; } = 6;
    public double MaxStageCoverage { get; init; } = 0.45;
    public double MinTargetSpacingFactor { get; init; } = 1.25;
    public double StageAreaX { get; init; } = 0.05;
    public double StageAreaY { get; init; } = 0.28;
    public double StageAreaWidth { get; init; } = 0.90;
    public double StageAreaHeight { get; init; } = 0.64;

    public void Validate()
    {
        if (Version != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Version), "Unsupported gameplay configuration version.");
        }

        if (RoundDurationSeconds is < 30 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundDurationSeconds), "Round duration must be between 30 and 90 seconds.");
        }

        if (CorrectScore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CorrectScore), "Correct score must be positive.");
        }

        ValidatePenalty(WrongSharpeningPenalty, nameof(WrongSharpeningPenalty));
        ValidatePenalty(ShaftHitPenalty, nameof(ShaftHitPenalty));
        ValidatePenalty(MissPenalty, nameof(MissPenalty));

        if (!double.IsFinite(ContactToleranceNormalized) || ContactToleranceNormalized is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ContactToleranceNormalized));
        }

        if (!double.IsFinite(MaxAngleDeviationDeg) || MaxAngleDeviationDeg is < 0 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAngleDeviationDeg));
        }

        if (!double.IsFinite(MinStrokeLengthNormalized) || MinStrokeLengthNormalized is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinStrokeLengthNormalized));
        }

        if (FailedDespawnMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FailedDespawnMs));
        }

        if (SharpDespawnMs <= 0 || MaxConcurrentTargets is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(SharpDespawnMs));
        }

        if (!double.IsFinite(MaxStageCoverage) || MaxStageCoverage is <= 0 or > 0.45
            || !double.IsFinite(MinTargetSpacingFactor) || MinTargetSpacingFactor < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxStageCoverage));
        }

        if (!double.IsFinite(StageAreaX)
            || !double.IsFinite(StageAreaY)
            || !double.IsFinite(StageAreaWidth)
            || !double.IsFinite(StageAreaHeight)
            || StageAreaX < 0
            || StageAreaY < 0
            || StageAreaWidth <= 0
            || StageAreaHeight <= 0
            || StageAreaX + StageAreaWidth > 1
            || StageAreaY + StageAreaHeight > 1
            || StageAreaWidth * StageAreaHeight < 0.55)
        {
            throw new ArgumentOutOfRangeException(nameof(StageAreaWidth));
        }
    }

    private static void ValidatePenalty(int value, string propertyName)
    {
        if (value > 0)
        {
            throw new ArgumentOutOfRangeException(propertyName, "Penalty values must not be positive.");
        }
    }
}