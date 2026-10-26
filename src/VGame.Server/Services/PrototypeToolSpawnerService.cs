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
            var placement = CreatePlacement(width, height, Random.Shared.Next(4));

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

    public static ToolPlacement CreatePlacement(double width, double height, int direction)
    {
        if (direction is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(direction));
        var halfExtents = ToolGeometry.HalfExtents(width, height, 0, true);
        var horizontal = direction < 2;
        var positive = direction % 2 == 0;
        var margin = (horizontal ? halfExtents.X : halfExtents.Y) + 0.04;
        var startAlong = positive ? -margin : 1 + margin;
        var endAlong = positive ? 1 + margin : -margin;
        var startAcross = 0.15 + Random.Shared.NextDouble() * 0.7;
        var endAcross = 0.15 + Random.Shared.NextDouble() * 0.7;
        var bend = (Random.Shared.NextDouble() - 0.5) * 0.16;
        Point2 Map(double along, double across) => horizontal ? new(along, across) : new(across, along);
        var start = Map(startAlong, startAcross);
        var curve = new CubicBezierPath(start,
            Map(startAlong + (endAlong - startAlong) / 3, startAcross + (endAcross - startAcross) / 3 + bend),
            Map(startAlong + (endAlong - startAlong) * 2 / 3, startAcross + (endAcross - startAcross) * 2 / 3 - bend),
            Map(endAlong, endAcross), 6 + Random.Shared.NextDouble() * 3);
        var angularSpeed = (Random.Shared.Next(2) == 0 ? -1 : 1) * (12 + Random.Shared.NextDouble() * 18);
        return new ToolPlacement(start, 0, 0, 0, curve, AngularVelocityDegreesPerSecond: angularSpeed);
    }
}