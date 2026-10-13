#if DEBUG
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;
using VGame.Contracts;
using VGame.Domain;
using VGame.Tools.Manifest;

namespace VGame.Server.Services;

public sealed record ZoneEditorAsset(string AssetId, string Category, int Width, int Height, string File, AssetZoneAuthoring Zones, string Version);
public sealed record ZonePreviewRequest(AssetZoneAuthoring Zones, string Mode, IReadOnlyList<Point2> Points);

public sealed class ZoneEditorService(string assetsDirectory, string sourceDirectory, IReadOnlyList<ToolDefinition> prototypes, GameplayOptions gameplay)
{
    private readonly object writeLock = new();

    public IReadOnlyList<string> ListAssets() => Directory.Exists(Path.Combine(assetsDirectory, "images/tools"))
        ? Directory.GetFiles(Path.Combine(assetsDirectory, "images/tools"))
            .Where(filename => Path.GetExtension(filename) is ".webp" or ".png")
            .Select(Path.GetFileNameWithoutExtension).Where(identifier => identifier is not null && Regex.IsMatch(identifier, "^tool_[0-9]{4}$"))
            .Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : [];

    public ZoneEditorAsset Read(string assetId)
    {
        using var bitmap = OpenBitmap(assetId);
        var filename = ZoneFile(assetId);
        AssetZoneAuthoring zones;
        if (File.Exists(filename)) zones = JsonSerializer.Deserialize<AssetZoneAuthoring>(File.ReadAllText(filename), ManifestInputs.JsonOptions)
            ?? throw new ArgumentException("Die Zonendatei ist leer.");
        else
        {
            var prototype = prototypes.FirstOrDefault(tool => tool.AssetId == assetId);
            zones = new() { AssetId = assetId, Canvas = new() { Width = bitmap.Width, Height = bitmap.Height }, Pivot = new(0.5, 0.5),
                HitZones = prototype?.HitZones.Select(zone => new ManifestHitZone { ZoneId = zone.ZoneId, Role = zone.Role == HitZoneRole.Cut ? "cut" : "shaft",
                    Points = zone.Points.Select(point => new[] { point.X, point.Y }).ToArray() }).ToArray()
                    ?? [new() { ZoneId = "cut", Role = "cut", Points = [] }, new() { ZoneId = "shaft", Role = "shaft", Points = [] }] };
        }
        return new(assetId, "tool-image", bitmap.Width, bitmap.Height, "/Assets/images/tools/" + Path.GetFileName(ImageFile(assetId)), zones, Version(filename));
    }

    public AssetZoneAuthoring Validate(string assetId, JsonElement document)
    {
        using var bitmap = OpenBitmap(assetId);
        var metadata = new AssetMetadata { AssetId = assetId, Category = "tool-image", LicenseStatus = "not-evaluated", Source = "zone-editor", CreatedUtc = DateTimeOffset.UnixEpoch };
        return AssetValidator.ValidateZones(document, metadata, bitmap);
    }

    public string Save(string assetId, JsonElement document, string expectedVersion)
    {
        var zones = Validate(assetId, document);
        var filename = ZoneFile(assetId);
        lock (writeLock)
        {
            if (expectedVersion != Version(filename)) throw new InvalidOperationException("Die Zonendatei wurde zwischenzeitlich geändert. Bitte neu laden.");
            Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
            var temporary = filename + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, ManifestInputs.CanonicalJson(zones));
                File.Move(temporary, filename, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return Version(filename);
        }
    }

    public ActionResolved Preview(ZonePreviewRequest request)
    {
        if (request.Zones is null) throw new ArgumentException("Zonendaten fehlen.");
        var zones = Validate(request.Zones.AssetId, JsonSerializer.SerializeToElement(request.Zones, ManifestInputs.JsonOptions));
        if (request.Mode is not ("tap" or "swipe") || request.Points is null || request.Points.Count is < 1 or > 32
            || request.Points.Any(point => point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X is < 0 or > 1 || point.Y is < 0 or > 1))
            throw new ArgumentException("Ungültige Testgeste.");
        var engine = new GameEngine(gameplay);
        engine.JoinClient(new("zone-preview", ClientRole.Player), true);
        engine.SelectMode("zone-preview", request.Mode == "tap" ? GameMode.Laser : GameMode.Saw);
        engine.StartRound("zone-preview");
        var prototype = prototypes.FirstOrDefault(tool => tool.AssetId == zones.AssetId);
        var definition = new ToolDefinition(zones.AssetId, 0, zones.HitZones.Select(zone => new HitZoneDefinition(zone.ZoneId,
            zone.Role == "shaft" ? HitZoneRole.Shaft : HitZoneRole.Cut, zone.Points.Select(pair => new Point2(pair[0], pair[1])).ToArray())).ToArray(),
            prototype?.CutDirectionDegrees ?? 0, 1, 1, prototype?.VisualKind ?? "drill", prototype?.HasPkd ?? false, zones.AssetId);
        engine.RegisterTool(definition);
        var result = engine.ResolveAction(new("zone-preview", 1, engine.GetSnapshot().RoundId!.Value, zones.AssetId, DateTimeOffset.UtcNow,
            new(request.Mode == "tap" ? GestureKind.Tap : GestureKind.Swipe, request.Points, 0, 250)));
        return result.Resolution ?? throw new ArgumentException("Die Testgeste konnte nicht ausgewertet werden.");
    }

    private SKBitmap OpenBitmap(string assetId) => SKBitmap.Decode(ImageFile(assetId)) ?? throw new ArgumentException("Das Werkzeugbild kann nicht gelesen werden.");
    private string ImageFile(string assetId)
    {
        CheckIdentifier(assetId);
        var files = new[] { ".webp", ".png" }.Select(extension => Path.Combine(assetsDirectory, "images/tools", assetId + extension)).Where(File.Exists).ToArray();
        return files.Length == 1 ? files[0] : throw new FileNotFoundException("Werkzeugbild fehlt oder ist nicht eindeutig.");
    }
    private string ZoneFile(string assetId)
    {
        CheckIdentifier(assetId);
        return Path.Combine(sourceDirectory, "zone-authoring", assetId + ".zones.json");
    }
    private static void CheckIdentifier(string assetId)
    {
        if (assetId is null || !Regex.IsMatch(assetId, "^tool_[0-9]{4}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Ungültige Werkzeug-ID.");
    }
    private static string Version(string filename) => File.Exists(filename) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(filename))) : "new";
}
#endif