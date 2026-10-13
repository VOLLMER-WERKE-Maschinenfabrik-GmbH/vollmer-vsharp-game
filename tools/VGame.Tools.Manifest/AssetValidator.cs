using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using SkiaSharp;
using VGame.Contracts;
using VGame.Domain;

namespace VGame.Tools.Manifest;

public sealed class AssetValidator
{
    private readonly BuildOptions schemaOptions = new() { SchemaRegistry = new SchemaRegistry() };
    private readonly Dictionary<string, (JsonSchema Schema, JsonObject Document)> templates = new(StringComparer.Ordinal);

    public AssetValidator(string templateDirectory)
    {
        _ = JsonSchema.FromFile(Path.Combine(templateDirectory, "asset.common.schema.json"), schemaOptions);
        foreach (var filename in Directory.GetFiles(templateDirectory, "*.template.json").Order(StringComparer.Ordinal))
        {
            var document = JsonNode.Parse(File.ReadAllText(filename))!.AsObject();
            var category = document["properties"]!["category"]!["const"]!.GetValue<string>();
            templates.Add(category, (JsonSchema.FromFile(filename, schemaOptions), document));
        }
    }

    public JsonObject GetTemplate(string category) => templates.TryGetValue(category, out var template)
        ? template.Document : throw new ManifestValidationException($"Unknown category: {category}.");

    public AssetMetadata ReadMetadata(string filename)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filename));
        var metadata = document.RootElement.Deserialize<AssetMetadata>(ManifestInputs.JsonOptions)
            ?? throw new ManifestValidationException($"{filename}: empty metadata.");
        _ = GetTemplate(metadata.Category);
        if (!templates[metadata.Category].Schema.Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid)
            throw new ManifestValidationException($"{metadata.AssetId}: metadata fails category schema.");
        if (Path.GetFileName(filename) != metadata.AssetId + ".meta.json")
            throw new ManifestValidationException($"{metadata.AssetId}: metadata filename does not match ID.");
        return metadata;
    }

    public static SKBitmap ValidateImage(string filename, JsonObject template)
    {
        var norm = template["x-imageNorm"]!.AsObject();
        using var stream = File.OpenRead(filename);
        using var codec = SKCodec.Create(stream) ?? throw new ManifestValidationException($"{filename}: invalid image.");
        var extension = Path.GetExtension(filename).TrimStart('.');
        var actualFormat = codec.EncodedFormat.ToString().ToLowerInvariant();
        if (extension != actualFormat || !norm["formats"]!.AsArray().Any(format => format!.GetValue<string>() == actualFormat))
            throw new ManifestValidationException($"{filename}: image format does not match category or extension.");
        var bitmap = SKBitmap.Decode(codec) ?? throw new ManifestValidationException($"{filename}: image cannot be decoded.");
        try
        {
            if ((norm["width"] is not null && bitmap.Width != norm["width"]!.GetValue<int>())
                || (norm["height"] is not null && bitmap.Height != norm["height"]!.GetValue<int>())
                || (norm["minimumLongestEdge"] is not null && Math.Max(bitmap.Width, bitmap.Height) < norm["minimumLongestEdge"]!.GetValue<int>()))
                throw new ManifestValidationException($"{filename}: image dimensions violate category norm.");
            if (norm["transparent"]?.GetValue<bool>() == true && bitmap.AlphaType == SKAlphaType.Opaque)
                throw new ManifestValidationException($"{filename}: transparent image required.");
            var opaquePixels = 0;
            var transparentBorder = true;
            var pixels = bitmap.Pixels;
            for (var vertical = 0; vertical < bitmap.Height; vertical++)
            for (var horizontal = 0; horizontal < bitmap.Width; horizontal++)
            {
                var alpha = pixels[vertical * bitmap.Width + horizontal].Alpha;
                if (alpha >= 128) opaquePixels++;
                if ((horizontal == 0 || vertical == 0 || horizontal == bitmap.Width - 1 || vertical == bitmap.Height - 1) && alpha > 0)
                    transparentBorder = false;
            }
            if (opaquePixels == 0) throw new ManifestValidationException($"{filename}: empty silhouette.");
            if (norm["transparent"]?.GetValue<bool>() == true && !transparentBorder)
                throw new ManifestValidationException($"{filename}: transparent border required.");
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public static void ValidateStageArea(ManifestStageArea area)
    {
        if (!double.IsFinite(area.HorizontalPosition) || !double.IsFinite(area.VerticalPosition)
            || !double.IsFinite(area.Width) || !double.IsFinite(area.Height)
            || area.HorizontalPosition < 0 || area.VerticalPosition < 0 || area.Width <= 0 || area.Height <= 0
            || area.HorizontalPosition + area.Width > 1 || area.VerticalPosition + area.Height > 1
            || area.Width * area.Height < 0.55)
            throw new ManifestValidationException("Stage area is outside the canvas or covers less than 55 percent.");
    }

    public static AssetZoneAuthoring ValidateZones(string filename, AssetMetadata metadata, SKBitmap bitmap)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filename));
        return ValidateZones(document.RootElement, metadata, bitmap);
    }

    public static AssetZoneAuthoring ValidateZones(JsonElement document, AssetMetadata metadata, SKBitmap bitmap)
    {
        if (!document.TryGetProperty("pivot", out var pivot) || pivot.ValueKind != JsonValueKind.Object
            || !pivot.TryGetProperty("x", out var horizontalPivot) || horizontalPivot.ValueKind != JsonValueKind.Number
            || !pivot.TryGetProperty("y", out var verticalPivot) || verticalPivot.ValueKind != JsonValueKind.Number)
            throw new ManifestValidationException($"{metadata.AssetId}: explicit numeric pivot coordinates required.");
        var zones = document.Deserialize<AssetZoneAuthoring>(ManifestInputs.JsonOptions)
            ?? throw new ManifestValidationException($"{metadata.AssetId}: empty zones.");
        if (zones.AssetId != metadata.AssetId || zones.Canvas is null || zones.Canvas.Width != bitmap.Width || zones.Canvas.Height != bitmap.Height
            || zones.Pivot is null || !IsNormalized(zones.Pivot.X) || !IsNormalized(zones.Pivot.Y)
            || zones.HitZones is null || zones.HitZones.Count != 2
            || zones.HitZones.Any(zone => zone is null || string.IsNullOrWhiteSpace(zone.ZoneId))
            || zones.HitZones.Select(zone => zone.ZoneId).Distinct(StringComparer.Ordinal).Count() != 2
            || zones.HitZones.Count(zone => zone.Role == "cut") != 1 || zones.HitZones.Count(zone => zone.Role == "shaft") != 1)
            throw new ManifestValidationException($"{metadata.AssetId}: canvas, pivot or cut/shaft roles invalid.");
        var polygons = zones.HitZones.Select(zone => CreatePolygon(zone, metadata.AssetId)).ToArray();
        var cuttingIndex = zones.HitZones[0].Role == "cut" ? 0 : 1;
        var shaftIndex = 1 - cuttingIndex;
        var reachableCut = polygons[cuttingIndex].Difference(polygons[shaftIndex]);
        if (reachableCut is not Polygon || reachableCut.IsEmpty || reachableCut.Area <= 0)
            throw new ManifestValidationException($"{metadata.AssetId}: cutting zone is unreachable or disconnected after shaft priority.");
        var locators = polygons.Select(polygon => new IndexedPointInAreaLocator(polygon)).ToArray();
        var coordinate = new Coordinate();
        var cuttingPixels = 0;
        var pixels = bitmap.Pixels;
        for (var vertical = 0; vertical < bitmap.Height; vertical++)
        for (var horizontal = 0; horizontal < bitmap.Width; horizontal++)
        {
            if (pixels[vertical * bitmap.Width + horizontal].Alpha < 128) continue;
            coordinate.X = (horizontal + 0.5) / bitmap.Width;
            coordinate.Y = (vertical + 0.5) / bitmap.Height;
            var cuttingContact = locators[cuttingIndex].Locate(coordinate) != Location.Exterior;
            var shaftContact = locators[shaftIndex].Locate(coordinate) != Location.Exterior;
            if (cuttingContact && !shaftContact) cuttingPixels++;
            if (!cuttingContact && !shaftContact) throw new ManifestValidationException($"{metadata.AssetId}: silhouette has uncovered opaque pixels.");
        }
        if (cuttingPixels == 0) throw new ManifestValidationException($"{metadata.AssetId}: cutting zone contains no opaque pixels.");
        return zones;
    }

    public static Polygon CreatePolygon(ManifestHitZone zone, string assetId)
    {
        if (zone.Points is null || zone.Points.Count < 3 || zone.Points.Any(point => point is null || point.Length != 2 || !IsNormalized(point[0]) || !IsNormalized(point[1])))
            throw new ManifestValidationException($"{assetId}: invalid polygon coordinates.");
        var coordinates = zone.Points.Select(point => new Coordinate(point[0], point[1])).ToList();
        if (!coordinates[0].Equals2D(coordinates[^1])) coordinates.Add(coordinates[0].Copy());
        var polygon = new GeometryFactory().CreatePolygon(coordinates.ToArray());
        if (!polygon.IsValid || polygon.IsEmpty || polygon.Area <= 0)
            throw new ManifestValidationException($"{assetId}: polygon is empty, disconnected or self-intersecting.");
        return polygon;
    }

    public static ZoneContact? Classify(AssetZoneAuthoring zones, Point2 point)
    {
        var definition = new ToolDefinition(zones.AssetId, 0, zones.HitZones.Select(zone =>
            new HitZoneDefinition(zone.ZoneId, zone.Role == "shaft" ? HitZoneRole.Shaft : HitZoneRole.Cut,
                zone.Points.Select(pair => new Point2(pair[0], pair[1])).ToArray())).ToArray(), 0, 1, 1);
        return HitZoneClassifier.FindFirstContact([new ToolSnapshot(definition, ToolState.Dull)], [point], 0);
    }

    private static bool IsNormalized(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}