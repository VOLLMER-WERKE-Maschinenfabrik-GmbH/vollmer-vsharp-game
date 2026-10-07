using Microsoft.AspNetCore.SignalR;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Hubs;

namespace VGame.Server.Services;

public sealed class PrototypeToolSpawnerService(
    GameEngine gameEngine,
    GameplayOptions options,
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
            var placement = CreatePlacement(snapshot.Tools, spawnSequence);
            if (placement is null)
            {
                continue;
            }

            var toolId = $"prototype_{snapshot.RoundId:N}_{spawnSequence:0000}";
            var isSawMode = snapshot.Mode == GameMode.Saw;
            var definition = new ToolDefinition(
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

    private ToolPlacement? CreatePlacement(IReadOnlyList<ToolSnapshot> activeTools, int spawnSequence)
    {
        var minimumX = options.StageAreaX + ToolWidthNormalized / 2;
        var maximumX = options.StageAreaX + options.StageAreaWidth - ToolWidthNormalized / 2;
        var minimumY = options.StageAreaY + ToolHeightNormalized / 2;
        var maximumY = options.StageAreaY + options.StageAreaHeight - ToolHeightNormalized / 2;
        var stageArea = options.StageAreaWidth * options.StageAreaHeight;
        var requestedArea = ToolWidthNormalized * ToolHeightNormalized;
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
            if (activeTools.Any(tool => IsTooClose(center, tool)))
            {
                continue;
            }

            var horizontalSpeed = direction * (0.07 + Random.Shared.NextDouble() * 0.05);
            var verticalSpeed = (Random.Shared.NextDouble() - 0.5) * 0.06;
            return new ToolPlacement(center, horizontalSpeed, verticalSpeed, 0);
        }

        return null;
    }

    private bool IsTooClose(Point2 center, ToolSnapshot other)
    {
        if (other.Placement is null)
        {
            return false;
        }

        var minimumDistance = options.MinTargetSpacingFactor
            * Math.Max(ToolWidthNormalized, Math.Max(other.Definition.BoundingWidthNormalized, other.Definition.BoundingHeightNormalized));
        var distanceX = center.X - other.Placement.Center.X;
        var distanceY = center.Y - other.Placement.Center.Y;
        return distanceX * distanceX + distanceY * distanceY < minimumDistance * minimumDistance;
    }
}