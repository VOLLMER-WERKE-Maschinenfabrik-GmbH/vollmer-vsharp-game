using Microsoft.AspNetCore.SignalR;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Hubs;

namespace VGame.Server.Services;

public sealed class PrototypeToolSpawnerService(
    GameEngine gameEngine,
    GameplayOptions options,
    IReadOnlyList<ToolDefinition> artworkTools,
    IHubContext<GameHub, IGameClient> hubContext) : BackgroundService
{
    private const double ToolWidthNormalized = 0.16;
    private const double ToolHeightNormalized = 0.14;
    private static readonly HitZoneDefinition ShaftZone = new(
        "shaft",
        HitZoneRole.Shaft,
        [new Point2(0.12, 0.28), new Point2(0.52, 0.28), new Point2(0.52, 0.72), new Point2(0.12, 0.72)]);
    private static readonly HitZoneDefinition CutZone = new(
        "cut",
        HitZoneRole.Cut,
        [new Point2(0.52, 0.2), new Point2(0.9, 0.2), new Point2(0.9, 0.8), new Point2(0.52, 0.8)]);
    private int _spawnSequence;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = gameEngine.GetSnapshot();
            if (snapshot.Phase != GamePhase.Playing || snapshot.Tools.Count >= options.MaxConcurrentTargets)
            {
                continue;
            }

            var spawnSequence = Interlocked.Increment(ref _spawnSequence);
            var isSawMode = snapshot.Mode == GameMode.Saw;
            var candidates = artworkTools.Where(tool => (tool.VisualKind == "saw") == isSawMode).ToArray();
            var artwork = candidates.Length > 0 ? candidates[(spawnSequence - 1) % candidates.Length] : null;
            var width = artwork?.BoundingWidthNormalized ?? ToolWidthNormalized;
            var height = artwork?.BoundingHeightNormalized ?? ToolHeightNormalized;
            var placement = CreatePlacement(snapshot.Tools, spawnSequence, width, height);
            if (placement is null)
            {
                continue;
            }

            var toolId = $"prototype_{snapshot.RoundId:N}_{spawnSequence:0000}";
            var definition = artwork is not null ? artwork with { ToolId = toolId, ZOrder = spawnSequence } : new ToolDefinition(
                toolId,
                spawnSequence,
                [ShaftZone, CutZone],
                0,
                ToolWidthNormalized,
                ToolHeightNormalized,
                isSawMode ? "saw" : Random.Shared.Next(2) == 0 ? "drill" : "cutter",
                !isSawMode && Random.Shared.Next(4) == 0);

            if (gameEngine.RegisterTool(definition, placement))
            {
                await hubContext.Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
            }
        }
    }

    private ToolPlacement? CreatePlacement(IReadOnlyList<ToolSnapshot> activeTools, int spawnSequence, double width, double height)
    {
        var minimumX = options.StageAreaX + width / 2;
        var maximumX = options.StageAreaX + options.StageAreaWidth - width / 2;
        var minimumY = options.StageAreaY + height / 2;
        var maximumY = options.StageAreaY + options.StageAreaHeight - height / 2;
        var stageArea = options.StageAreaWidth * options.StageAreaHeight;
        var requestedArea = width * height;
        var usedArea = activeTools.Sum(tool => tool.Definition.BoundingWidthNormalized * tool.Definition.BoundingHeightNormalized);

        if ((usedArea + requestedArea) / stageArea > options.MaxStageCoverage)
        {
            return null;
        }

        var direction = spawnSequence % 2 == 0 ? 1 : -1;
        var centerX = direction > 0 ? minimumX : maximumX;
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var center = new Point2(
                centerX,
                minimumY + Random.Shared.NextDouble() * (maximumY - minimumY));
            if (activeTools.Any(tool => IsTooClose(center, tool, width, height)))
            {
                continue;
            }

            var horizontalSpeed = direction * (0.07 + Random.Shared.NextDouble() * 0.05);
            var horizontalRange = maximumX - minimumX;
            var verticalRange = maximumY - minimumY;
            var firstBand = spawnSequence % 3 == 0 ? 0.65 : 0.05;
            var secondBand = spawnSequence % 3 == 1 ? 0.05 : 0.65;
            var curve = new CubicBezierPath(center,
                new Point2(centerX + direction * horizontalRange * 0.3, minimumY + verticalRange * (firstBand + Random.Shared.NextDouble() * 0.3)),
                new Point2(centerX + direction * horizontalRange * 0.7, minimumY + verticalRange * (secondBand + Random.Shared.NextDouble() * 0.3)),
                new Point2(direction > 0 ? maximumX : minimumX, minimumY + verticalRange * (0.15 + Random.Shared.NextDouble() * 0.7)),
                horizontalRange / Math.Abs(horizontalSpeed));
            return new ToolPlacement(center, 0, 0, 0, curve);
        }

        return null;
    }

    private bool IsTooClose(Point2 center, ToolSnapshot other, double width, double height)
    {
        if (other.Placement is null)
        {
            return false;
        }

        var minimumDistance = options.MinTargetSpacingFactor
            * Math.Max(Math.Max(width, height), Math.Max(other.Definition.BoundingWidthNormalized, other.Definition.BoundingHeightNormalized));
        var distanceX = center.X - other.Placement.Center.X;
        var distanceY = center.Y - other.Placement.Center.Y;
        return distanceX * distanceX + distanceY * distanceY < minimumDistance * minimumDistance;
    }
}