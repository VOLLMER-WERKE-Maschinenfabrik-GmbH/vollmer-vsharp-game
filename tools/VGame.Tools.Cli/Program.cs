using System.CommandLine;
using System.Globalization;
using VGame.Tools.Manifest;

namespace VGame.Tools.Cli;

public static class Program
{
    public static int Main(string[] arguments)
    {
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        return Run(arguments, Console.Out);
    }

    public static int Run(string[] arguments, TextWriter output)
    {
        var root = new RootCommand("Build and verify the generated VGame asset manifest.");
        var manifest = new Command("manifest", "Manifest generation and validation.");
        root.Subcommands.Add(manifest);
        root.SetAction(_ => ShowUsage(output));
        manifest.SetAction(_ => ShowUsage(output));
        var audio = new Command("audio", "Audio authoring tools.");
        var pools = new Command("pools", "Derive pool variants from existing audio files.");
        root.Subcommands.Add(audio);
        audio.Subcommands.Add(pools);
        audio.SetAction(_ => ShowUsage(output));
        pools.SetAction(_ => ShowUsage(output));
        foreach (var (commandName, parent, write) in new[] { ("build", manifest, true), ("verify", manifest, false), ("build", pools, true) })
        {
            var command = new Command(commandName, commandName == "build" ? "Generate validated manifest files." : "Validate without writing files.");
            var assets = new Option<string>("--assets") { Description = "Runtime asset directory.", DefaultValueFactory = _ => "Assets" };
            var source = new Option<string>("--source") { Description = "Authoring source directory.", DefaultValueFactory = _ => "media-source" };
            var destination = new Option<string?>("--out") { Description = "Output path; must be Assets/config/manifest.json." };
            command.Options.Add(assets);
            command.Options.Add(source);
            command.Options.Add(destination);
            command.SetAction(parseResult =>
            {
                var assetsDirectory = Path.GetFullPath(parseResult.GetValue(assets)!);
                var sourceDirectory = Path.GetFullPath(parseResult.GetValue(source)!);
                var filename = parseResult.GetValue(destination) ?? Path.Combine(assetsDirectory, "config/manifest.json");
                var result = new ManifestBuilder().Run(new(assetsDirectory, sourceDirectory, filename), write);
                foreach (var issue in result.Issues) output.WriteLine($"{(issue.Planned ? "PLANNED" : "ERROR")} {issue.AssetId}: {issue.Message}");
                output.WriteLine($"Manifest {commandName}: exit {result.ExitCode}; files written: {result.Written.ToString().ToLowerInvariant()}.");
                return result.ExitCode;
            });
            parent.Subcommands.Add(command);
        }
        var parsed = root.Parse(arguments);
        if (parsed.Errors.Count > 0)
        {
            foreach (var error in parsed.Errors) output.WriteLine(error.Message);
            return 2;
        }
        try { return parsed.Invoke(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            output.WriteLine(exception.Message);
            return 3;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            output.WriteLine(exception.Message);
            return 2;
        }
    }

    private static int ShowUsage(TextWriter output)
    {
        output.WriteLine("Usage: vgtools manifest build|verify [--assets Assets] [--source media-source] [--out Assets/config/manifest.json]");
        output.WriteLine("       vgtools audio pools build [--assets Assets] [--source media-source] [--out Assets/config/manifest.json]");
        return 2;
    }
}