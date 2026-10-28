using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VGame.Contracts;

namespace VGame.Tools.Manifest;

public sealed record ManifestPaths(string AssetsDirectory, string SourceDirectory, string OutputFile)
{
    public void Validate()
    {
        var configurationDirectory = Path.GetFullPath(Path.Combine(AssetsDirectory, "config"));
        var output = Path.GetFullPath(OutputFile);
        if (!string.Equals(Path.GetDirectoryName(output), configurationDirectory, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(output), "manifest.json", StringComparison.Ordinal))
        {
            throw new ManifestValidationException("Manifest output must be Assets/config/manifest.json.");
        }
        var source = Path.GetFullPath(SourceDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (output.StartsWith(source, StringComparison.OrdinalIgnoreCase))
        {
            throw new ManifestValidationException("Manifest output must not modify authoring sources.");
        }
    }
}

public sealed class ManifestValidationException(string message) : Exception(message);

public static class ManifestInputs
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyDictionary<string, AssetIdReservation> ReadRegister(string filename)
    {
        var register = JsonSerializer.Deserialize<AssetIdRegister>(File.ReadAllText(filename), JsonOptions)
            ?? throw new ManifestValidationException("ID register is empty.");
        if (register.Version != 1 || register.Entries is null)
            throw new ManifestValidationException("Unsupported or incomplete ID register.");
        var result = new Dictionary<string, AssetIdReservation>(StringComparer.Ordinal);
        foreach (var entry in register.Entries)
        {
            if (entry is null || entry.AssetId is null || entry.Category is null
                || !Regex.IsMatch(entry.AssetId, "^[a-z]+_[0-9]{4}$", RegexOptions.CultureInvariant)
                || entry.Status is not ("reserved" or "assigned") || !result.TryAdd(entry.AssetId, entry))
            {
                throw new ManifestValidationException("Duplicate, malformed or unknown ID reservation.");
            }
        }
        return result;
    }

    public static void ValidateAssignment(AssetMetadata metadata, IReadOnlyDictionary<string, AssetIdReservation> register)
    {
        if (!register.TryGetValue(metadata.AssetId, out var reservation)
            || reservation.Category != metadata.Category || reservation.Status != "assigned")
        {
            throw new ManifestValidationException($"{metadata.AssetId}: ID is missing, reserved or assigned to another category.");
        }
    }

    public static string CanonicalJson<T>(T value) => Sort(JsonSerializer.SerializeToNode(value, JsonOptions))!.ToJsonString(JsonOptions).Replace("\r\n", "\n") + "\n";

    private static JsonNode? Sort(JsonNode? value) => value switch
    {
        JsonObject document => new JsonObject(document.OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => KeyValuePair.Create(property.Key, Sort(property.Value))).ToArray()),
        JsonArray items => new JsonArray(items.Select(Sort).ToArray()),
        _ => value?.DeepClone()
    };
}