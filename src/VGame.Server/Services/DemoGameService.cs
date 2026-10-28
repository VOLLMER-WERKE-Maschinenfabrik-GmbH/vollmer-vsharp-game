using VGame.Contracts;
using VGame.Domain;

namespace VGame.Server.Services;

public sealed class DemoGameService(GameEngine realGame, GameplayOptions options, TimeProvider timeProvider,
    IReadOnlyList<ToolDefinition> artwork)
{
    private const long SegmentMilliseconds = 60_000;
    private const string DemoClientId = "demo-computer";
    private static readonly int[] SampleSteps = [10, 18, 2, 15, 5, 19, 1, 12, 8, 17, 3, 13, 7, 16, 4, 14, 6, 11, 9];
    private readonly object _sync = new();
    private readonly GameplayOptions _options = options with { RoundDurationSeconds = 60 };
    private long? _sequenceStarted;
    private long _segment = -1;
    private long _lastActionAt = -1;
    private long _commandSequence;
    private int _spawnSequence;
    private GameEngine? _demoGame;
    private DemoSnapshot _snapshot = new("logo", SegmentMilliseconds, null, []);

    public DemoSnapshot GetSnapshot()
    {
        lock (_sync) return _snapshot;
    }

    public DemoSnapshot Advance()
    {
        lock (_sync)
        {
            if (realGame.GetSnapshot().Phase == GamePhase.Playing)
            {
                _sequenceStarted = null;
                _segment = -1;
                _demoGame = null;
                return _snapshot = new DemoSnapshot("inactive", 0, null, []);
            }
            var timestamp = timeProvider.GetTimestamp();
            _sequenceStarted ??= timestamp;
            var elapsed = Math.Max(0, (long)timeProvider.GetElapsedTime(_sequenceStarted.Value, timestamp).TotalMilliseconds);
            var segment = elapsed / SegmentMilliseconds;
            var withinSegment = elapsed % SegmentMilliseconds;
            var remaining = SegmentMilliseconds - withinSegment;
            if (_segment != segment)
            {
                _segment = segment;
                _demoGame = null;
                _lastActionAt = -1;
                _spawnSequence = 0;
                _commandSequence = 0;
                if (segment % 2 == 1)
                {
                    _demoGame = new GameEngine(_options, timeProvider);
                    _demoGame.JoinClient(new JoinRequest(DemoClientId, ClientRole.Player), true);
                    _demoGame.SelectMode(DemoClientId, segment % 4 == 1 ? GameMode.Laser : GameMode.Saw);
                    _demoGame.StartRound(DemoClientId);
                }
            }
            if (_demoGame is null) return _snapshot = new DemoSnapshot("logo", remaining, null, []);

            _demoGame.AdvanceClock();
            ActionResolved? action = null;
            if (withinSegment >= 1600 && (_lastActionAt < 0 || withinSegment - _lastActionAt >= 850))
            {
                _lastActionAt = withinSegment;
                action = PlayAction();
            }
            FillMinimumTargets();
            var snapshot = _demoGame.GetSnapshot() with { RemainingMs = remaining };
            return _snapshot = new DemoSnapshot(snapshot.Mode == GameMode.Laser ? "laser" : "saw", remaining,
                snapshot, action is null ? [] : [action]);
        }
    }

    private void FillMinimumTargets()
    {
        var snapshot = _demoGame!.GetSnapshot();
        var candidates = artwork.Where(tool => (tool.VisualKind == "saw") == (snapshot.Mode == GameMode.Saw)).ToArray();
        if (candidates.Length == 0) return;

        while (snapshot.Tools.Count(tool => tool.State == ToolState.Dull) < _options.MinimumConcurrentTargets)
        {
            var sequence = ++_spawnSequence;
            var definition = candidates[(sequence - 1) % candidates.Length] with
            {
                ToolId = $"demo_{snapshot.RoundId:N}_{sequence}", ZOrder = sequence
            };
            var placement = PrototypeToolSpawnerService.CreatePlacement(definition.BoundingWidthNormalized,
                definition.BoundingHeightNormalized, (sequence - 1) % 4);
            if (!_demoGame.RegisterTool(definition, placement)) break;
            snapshot = _demoGame.GetSnapshot();
        }
    }

    private ActionResolved? PlayAction()
    {
        var snapshot = _demoGame!.GetSnapshot();
        var mistake = (_commandSequence + 1) % 6;
        Point2[]? points = null;
        string? toolId = null;
        var kind = snapshot.Mode == GameMode.Saw ? GestureKind.Swipe : GestureKind.Tap;
        if (mistake == 3)
        {
            foreach (var corner in new[] { new Point2(0.03, 0.03), new Point2(0.97, 0.03), new Point2(0.03, 0.97), new Point2(0.97, 0.97) })
            {
                var path = kind == GestureKind.Tap ? new[] { corner } : new[] { corner, new Point2(corner.X + 0.01, corner.Y) };
                if (HitZoneClassifier.FindFirstContact(snapshot.Tools, path, _options.ContactToleranceNormalized) is not null) continue;
                points = path;
                toolId = "demo-miss";
                break;
            }
        }
        else
        {
            var role = mistake == 0 ? HitZoneRole.Shaft : HitZoneRole.Cut;
            foreach (var tool in snapshot.Tools.Where(tool => tool.State == ToolState.Dull && tool.Placement is not null)
                .OrderByDescending(tool => tool.Definition.ZOrder))
            {
                var point = FindPoint(snapshot.Tools, tool, role);
                if (point is null) continue;
                if (kind == GestureKind.Tap) points = [point];
                else if (role == HitZoneRole.Cut)
                {
                    points = FindSawSwipe(tool);
                    if (points is null) continue;
                }
                else
                {
                    points = [
                        TransformToolPoint(new Point2(0, 0.5), tool),
                        TransformToolPoint(new Point2(1, 0.5), tool)
                    ];
                }
                toolId = tool.Definition.ToolId;
                break;
            }
        }
        if (points is null || toolId is null) return null;
        var resolution = _demoGame.ResolveAction(new ActionRequested(DemoClientId, ++_commandSequence,
            snapshot.RoundId!.Value, toolId, timeProvider.GetUtcNow(), new GesturePayload(kind, points, 0, kind == GestureKind.Tap ? 0 : 240)));
        return resolution.Resolution;
    }

    private Point2? FindPoint(IReadOnlyList<ToolSnapshot> tools, ToolSnapshot tool, HitZoneRole role)
    {
        var placement = tool.Placement!;
        if (placement.Center.X is < 0.15 or > 0.85 || placement.Center.Y is < 0.15 or > 0.85) return null;
        var angle = placement.RotationDegrees * Math.PI / 180;
        foreach (var horizontal in SampleSteps)
        {
            foreach (var vertical in SampleSteps)
            {
                var localX = (horizontal / 20d - 0.5) * tool.Definition.BoundingWidthNormalized;
                var localY = (vertical / 20d - 0.5) * tool.Definition.BoundingHeightNormalized / ToolGeometry.CanvasAspectRatio;
                var point = new Point2(placement.Center.X + localX * Math.Cos(angle) - localY * Math.Sin(angle),
                    placement.Center.Y + (localX * Math.Sin(angle) + localY * Math.Cos(angle)) * ToolGeometry.CanvasAspectRatio);
                if (point.X is < 0.04 or > 0.96 || point.Y is < 0.04 or > 0.96) continue;
                var contact = HitZoneClassifier.FindFirstContact(tools, [point], _options.ContactToleranceNormalized);
                if (contact?.ToolId == tool.Definition.ToolId && contact.HitZone.Role == role) return point;
            }
        }
        return null;
    }

    private Point2[]? FindSawSwipe(ToolSnapshot tool)
    {
        var placement = tool.Placement!;
        var toolTargets = new[] { tool };
        var maximumDimension = Math.Max(tool.Definition.BoundingWidthNormalized, tool.Definition.BoundingHeightNormalized);
        var halfLength = maximumDimension * 0.75;
        for (var angleStep = 0; angleStep < 12; angleStep++)
        {
            var angle = angleStep * Math.PI / 12;
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var normalX = -directionY;
            var normalY = directionX;
            for (var offsetStep = -9; offsetStep <= 9; offsetStep++)
            {
                var offset = maximumDimension * offsetStep / 20;
                var start = TransformToolPoint(new Point2(
                    0.5 + (normalX * offset - directionX * halfLength) / tool.Definition.BoundingWidthNormalized,
                    0.5 + (normalY * offset - directionY * halfLength) / tool.Definition.BoundingHeightNormalized), tool);
                var end = TransformToolPoint(new Point2(
                    0.5 + (normalX * offset + directionX * halfLength) / tool.Definition.BoundingWidthNormalized,
                    0.5 + (normalY * offset + directionY * halfLength) / tool.Definition.BoundingHeightNormalized), tool);
                if (start.X is < 0 or > 1 || start.Y is < 0 or > 1 || end.X is < 0 or > 1 || end.Y is < 0 or > 1)
                {
                    continue;
                }

                var path = new[] { start, end };
                if (HitZoneClassifier.FindFirstContact(toolTargets, [start], 0) is not null
                    || HitZoneClassifier.FindFirstContact(toolTargets, [end], 0) is not null
                    || HitZoneClassifier.FindFirstContact(toolTargets, path, 0, HitZoneRole.Shaft) is not null
                    || HitZoneClassifier.FindFirstContact(toolTargets, path, _options.ContactToleranceNormalized, HitZoneRole.Cut) is null)
                {
                    continue;
                }

                return path;
            }
        }

        return null;
    }

    private static Point2 TransformToolPoint(Point2 point, ToolSnapshot tool)
    {
        var placement = tool.Placement!;
        var angle = placement.RotationDegrees * Math.PI / 180;
        var localX = (point.X - 0.5) * tool.Definition.BoundingWidthNormalized;
        var localY = (point.Y - 0.5) * tool.Definition.BoundingHeightNormalized / ToolGeometry.CanvasAspectRatio;
        return new Point2(
            placement.Center.X + localX * Math.Cos(angle) - localY * Math.Sin(angle),
            placement.Center.Y + (localX * Math.Sin(angle) + localY * Math.Cos(angle)) * ToolGeometry.CanvasAspectRatio);
    }
}