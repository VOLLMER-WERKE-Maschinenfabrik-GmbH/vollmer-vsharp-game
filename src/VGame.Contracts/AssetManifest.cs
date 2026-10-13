using System.Text.Json;
using System.Text.Json.Serialization;

namespace VGame.Contracts;

public sealed record AssetManifest
{
    public required string ManifestVersion { get; init; }
    public required string ConfigVersion { get; init; }
    public required IReadOnlyList<ToolManifestEntry> Tools { get; init; }
    public required IReadOnlyList<AudioPoolManifestEntry> AudioPools { get; init; }
    public required IReadOnlyList<StageManifestEntry> Stage { get; init; }
    public required IReadOnlyList<SpawnProfileManifestEntry> SpawnProfiles { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalCategories { get; init; }
}

public sealed record ToolManifestEntry
{
    public required string ToolId { get; init; }
    public required string Category { get; init; }
    public required string ToolType { get; init; }
    public required string Interaction { get; init; }
    public required string SizeClass { get; init; }
    public required bool Pkd { get; init; }
    public required string VisualAssetId { get; init; }
    public required Point2 Pivot { get; init; }
    public required IReadOnlyList<ManifestHitZone> HitZones { get; init; }
    [JsonPropertyName("cutDirectionDeg")]
    public double? CutDirectionDegrees { get; init; }
    [JsonPropertyName("minStrokeLengthNormalized")]
    public double? MinimumStrokeLengthNormalized { get; init; }
    public required IReadOnlyList<string> StateOverlays { get; init; }
    public required string ScoreProfileId { get; init; }
    public required ToolAudioPoolReferences AudioPools { get; init; }
    public required string LicenseStatus { get; init; }
}

public sealed record ManifestHitZone
{
    public required string ZoneId { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<double[]> Points { get; init; }
}

public sealed record ToolAudioPoolReferences
{
    public required string Success { get; init; }
    public required string Fail { get; init; }
}

public sealed record AudioPoolManifestEntry
{
    public required string PoolId { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyList<string> Variants { get; init; }
    public required string VolumeProfile { get; init; }
    public required string BagRule { get; init; }
    public required string Fallback { get; init; }
}

public sealed record StageManifestEntry
{
    public required string AssetId { get; init; }
    public required string Category { get; init; }
    public required string File { get; init; }
    public required string Layer { get; init; }
    public string? LicenseStatus { get; init; }
    public double? ParallaxFactor { get; init; }
    public ManifestStageArea? StageArea { get; init; }
    public IReadOnlyDictionary<string, string>? Palette { get; init; }
    public string? LightProfile { get; init; }
    public string? Sprite { get; init; }
    public string? MotionPath { get; init; }
    public string? SpeedProfile { get; init; }
    [JsonPropertyName("cooldownMs")]
    public int? CooldownMilliseconds { get; init; }
    public bool? Enabled { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record ManifestStageArea
{
    [JsonPropertyName("x")]
    public required double HorizontalPosition { get; init; }
    [JsonPropertyName("y")]
    public required double VerticalPosition { get; init; }
    [JsonPropertyName("w")]
    public required double Width { get; init; }
    [JsonPropertyName("h")]
    public required double Height { get; init; }
}

public sealed record SpawnProfileManifestEntry
{
    public required string SpawnProfileId { get; init; }
    public required int MaxConcurrentTargets { get; init; }
    public required double MaxStageCoverage { get; init; }
    [JsonPropertyName("minTargetSpacingFactor")]
    public required double MinimumTargetSpacingFactor { get; init; }
    public required IReadOnlyList<string> EntryEdges { get; init; }
    public required string DifficultyRampId { get; init; }
}

public sealed record AssetMetadata
{
    public required string AssetId { get; init; }
    public required string Category { get; init; }
    public required string LicenseStatus { get; init; }
    public required string Source { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public string? ToolType { get; init; }
    public string? Interaction { get; init; }
    public string? SizeClass { get; init; }
    public bool? Pkd { get; init; }
    public IReadOnlyList<string>? States { get; init; }
    public string? Model { get; init; }
    public long? Seed { get; init; }
    public string? Prompt { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? CategoryProperties { get; init; }
}

public sealed record AssetZoneAuthoring
{
    public required string AssetId { get; init; }
    public required AssetCanvasSize Canvas { get; init; }
    public required Point2 Pivot { get; init; }
    public required IReadOnlyList<ManifestHitZone> HitZones { get; init; }
}

public sealed record AssetCanvasSize
{
    public required int Width { get; init; }
    public required int Height { get; init; }
}

public sealed record AssetIdRegister
{
    public required int Version { get; init; }
    public required IReadOnlyList<AssetIdReservation> Entries { get; init; }
}

public sealed record AssetIdReservation
{
    public required string AssetId { get; init; }
    public required string Category { get; init; }
    public required string Status { get; init; }
}