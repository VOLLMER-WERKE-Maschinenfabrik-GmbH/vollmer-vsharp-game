using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VGame.Contracts;

namespace VGame.Tools.Manifest;

public static class AudioPoolBuilder
{
    public static IReadOnlyList<AudioPoolManifestEntry> Read(string assetsDirectory)
    {
        var options = new JsonSerializerOptions(ManifestInputs.JsonOptions) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var authoring = JsonSerializer.Deserialize<AudioPoolAuthoring>(File.ReadAllText(Path.Combine(assetsDirectory, "config/audio.pools.json")), options)
            ?? throw new ManifestValidationException("Audio pool definitions are empty.");
        return Build(Path.Combine(assetsDirectory, "audio"), authoring);
    }

    public static IReadOnlyList<AudioPoolManifestEntry> Build(string audioDirectory, AudioPoolAuthoring authoring)
    {
        if (authoring.Pools is null) throw new ManifestValidationException("Audio pools are missing.");
        var definitions = new Dictionary<string, AudioPoolDefinition>(StringComparer.Ordinal);
        foreach (var definition in authoring.Pools)
        {
            if (definition is null || !IsIdentifier(definition.PoolId) || !IsIdentifier(definition.FileGroup)
                || !IsIdentifier(definition.Category) || !IsIdentifier(definition.VolumeProfile)
                || !IsIdentifier(definition.Fallback) || definition.PoolId == "generated_tone"
                || definition.BagRule != "shuffle_no_repeat" || !definitions.TryAdd(definition.PoolId, definition))
                throw new ManifestValidationException("Audio pool has missing/invalid fields, unsupported bag rule or duplicate ID.");
        }
        foreach (var definition in definitions.Values)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { definition.PoolId };
            var fallback = definition.Fallback;
            while (fallback != "generated_tone")
            {
                if (!visited.Add(fallback)) throw new ManifestValidationException($"{definition.PoolId}: cyclic audio fallback.");
                if (!definitions.TryGetValue(fallback, out var target)) throw new ManifestValidationException($"{definition.PoolId}: unknown audio fallback.");
                fallback = target.Fallback;
            }
        }
        var filenames = Directory.Exists(audioDirectory)
            ? Directory.GetFiles(audioDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray() : [];
        var result = new List<AudioPoolManifestEntry>();
        foreach (var definition in definitions.Values.OrderBy(definition => definition.PoolId, StringComparer.Ordinal))
        {
            var variants = new SortedDictionary<int, string>();
            const string pattern = "^(?<group>[a-z][a-z0-9_-]*)_(?<number>[0-9]{2})\\.(wav|mp3|ogg)$";
            foreach (var filename in filenames)
            {
                var name = Path.GetFileName(filename);
                if (!name.StartsWith(definition.FileGroup + "_", StringComparison.Ordinal)) continue;
                var match = Regex.Match(name, pattern, RegexOptions.CultureInvariant);
                if (!match.Success) throw new ManifestValidationException($"{definition.PoolId}: invalid audio variant filename {name}.");
                var fileGroup = match.Groups["group"].Value;
                if (fileGroup != definition.FileGroup)
                {
                    if (definitions.Values.Any(other => other.FileGroup == fileGroup)) continue;
                    throw new ManifestValidationException($"{definition.PoolId}: unexpected audio file group {fileGroup}.");
                }
                var number = int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (number == 0 || !variants.TryAdd(number, Path.GetRelativePath(audioDirectory, filename).Replace('\\', '/')))
                    throw new ManifestValidationException($"{definition.PoolId}: duplicate or invalid audio variant number.");
            }
            result.Add(new() { PoolId = definition.PoolId, Category = definition.Category, Variants = variants.Values.ToArray(),
                VolumeProfile = definition.VolumeProfile, BagRule = definition.BagRule, Fallback = definition.Fallback });
        }
        return result;
    }

    private static bool IsIdentifier(string? value) => value is not null
        && Regex.IsMatch(value, "^[a-z][a-z0-9_-]*$", RegexOptions.CultureInvariant);
}