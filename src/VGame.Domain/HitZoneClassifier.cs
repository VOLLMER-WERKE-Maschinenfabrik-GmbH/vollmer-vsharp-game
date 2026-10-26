using VGame.Contracts;

namespace VGame.Domain;

public sealed record ZoneContact(string ToolId, HitZoneDefinition HitZone, double PathProgress, int ZOrder);

public static class HitZoneClassifier
{
    public static ZoneContact? FindFirstContact(
        IReadOnlyList<ToolSnapshot> tools,
        IReadOnlyList<Point2> path,
        double toleranceNormalized)
    {
        if (path.Count == 0 || !double.IsFinite(toleranceNormalized) || toleranceNormalized < 0)
        {
            return null;
        }

        if (toleranceNormalized > 0)
        {
            var directContact = FindFirstContact(tools, [path[0]], 0);
            if (directContact is not null) return directContact;
        }

        ZoneContact? firstContact = null;
        foreach (var tool in tools)
        {
            if (tool.State != ToolState.Dull)
            {
                continue;
            }

            foreach (var hitZone in tool.Definition.HitZones)
            {
                var transformedPoints = TransformZone(hitZone.Points, tool);
                if (!TryGetPathContact(path, transformedPoints, toleranceNormalized, out var pathProgress))
                {
                    continue;
                }

                var contact = new ZoneContact(tool.Definition.ToolId, hitZone, pathProgress, tool.Definition.ZOrder);
                if (IsEarlier(contact, firstContact))
                {
                    firstContact = contact;
                }
            }
        }

        return firstContact;
    }

    private static IReadOnlyList<Point2> TransformZone(IReadOnlyList<Point2> points, ToolSnapshot tool)
    {
        if (tool.Placement is null)
        {
            return points;
        }

        var placement = tool.Placement;
        var angleRadians = placement.RotationDegrees * Math.PI / 180;
        var cosine = Math.Cos(angleRadians);
        var sine = Math.Sin(angleRadians);
        return points.Select(point =>
        {
            var localX = (point.X - 0.5) * tool.Definition.BoundingWidthNormalized;
            var localY = (point.Y - 0.5) * tool.Definition.BoundingHeightNormalized / ToolGeometry.CanvasAspectRatio;
            return new Point2(
                placement.Center.X + localX * cosine - localY * sine,
                placement.Center.Y + (localX * sine + localY * cosine) * ToolGeometry.CanvasAspectRatio);
        }).ToArray();
    }

    private static bool IsEarlier(ZoneContact candidate, ZoneContact? current)
    {
        if (current is null || candidate.PathProgress < current.PathProgress - 1e-9)
        {
            return true;
        }

        if (Math.Abs(candidate.PathProgress - current.PathProgress) > 1e-9)
        {
            return false;
        }

        if (candidate.ToolId != current.ToolId && candidate.ZOrder != current.ZOrder)
        {
            return candidate.ZOrder > current.ZOrder;
        }

        if (candidate.HitZone.Role != current.HitZone.Role)
        {
            return candidate.HitZone.Role == HitZoneRole.Shaft;
        }

        return candidate.ZOrder > current.ZOrder;
    }

    private static bool TryGetPathContact(
        IReadOnlyList<Point2> path,
        IReadOnlyList<Point2> polygon,
        double tolerance,
        out double pathProgress)
    {
        pathProgress = double.PositiveInfinity;
        if (polygon.Count < 3)
        {
            return false;
        }

        if (path.Count == 1)
        {
            if (!IsInsidePolygon(path[0], polygon)
                && polygon.Zip(polygon.Skip(1).Append(polygon[0]), (start, end) => DistanceToSegment(path[0], start, end)).Min() > tolerance)
            {
                return false;
            }

            pathProgress = 0;
            return true;
        }

        for (var pathIndex = 0; pathIndex < path.Count - 1; pathIndex++)
        {
            var start = path[pathIndex];
            var end = path[pathIndex + 1];
            var segmentProgress = double.PositiveInfinity;

            if (IsInsidePolygon(start, polygon))
            {
                segmentProgress = 0;
            }
            else
            {
                foreach (var (edgeStart, edgeEnd) in GetEdges(polygon))
                {
                    var (distance, closestPathParameter) = SegmentDistance(start, end, edgeStart, edgeEnd);
                    if (distance > tolerance)
                    {
                        continue;
                    }

                    var contactParameter = FindToleranceEntry(start, end, edgeStart, edgeEnd, closestPathParameter, tolerance);
                    segmentProgress = Math.Min(segmentProgress, contactParameter);
                }
            }

            if (!double.IsPositiveInfinity(segmentProgress))
            {
                var progress = (pathIndex + segmentProgress) / (path.Count - 1);
                pathProgress = Math.Min(pathProgress, progress);
                return true;
            }
        }

        return false;
    }

    private static double FindToleranceEntry(
        Point2 pathStart,
        Point2 pathEnd,
        Point2 edgeStart,
        Point2 edgeEnd,
        double closestParameter,
        double tolerance)
    {
        if (DistanceToSegment(pathStart, edgeStart, edgeEnd) <= tolerance)
        {
            return 0;
        }

        var lower = 0d;
        var upper = closestParameter;
        for (var iteration = 0; iteration < 24; iteration++)
        {
            var middle = (lower + upper) / 2;
            var point = Interpolate(pathStart, pathEnd, middle);
            if (DistanceToSegment(point, edgeStart, edgeEnd) <= tolerance)
            {
                upper = middle;
            }
            else
            {
                lower = middle;
            }
        }

        return upper;
    }

    private static (double Distance, double PathParameter) SegmentDistance(
        Point2 pathStart,
        Point2 pathEnd,
        Point2 edgeStart,
        Point2 edgeEnd)
    {
        if (TryGetIntersectionParameter(pathStart, pathEnd, edgeStart, edgeEnd, out var intersectionParameter))
        {
            return (0, intersectionParameter);
        }

        var candidates = new[]
        {
            (Distance: DistanceToSegment(pathStart, edgeStart, edgeEnd), PathParameter: 0d),
            (Distance: DistanceToSegment(pathEnd, edgeStart, edgeEnd), PathParameter: 1d),
            (Distance: DistanceToSegment(edgeStart, pathStart, pathEnd), PathParameter: ProjectParameter(edgeStart, pathStart, pathEnd)),
            (Distance: DistanceToSegment(edgeEnd, pathStart, pathEnd), PathParameter: ProjectParameter(edgeEnd, pathStart, pathEnd))
        };

        return candidates.MinBy(candidate => candidate.Distance);
    }

    private static bool TryGetIntersectionParameter(
        Point2 firstStart,
        Point2 firstEnd,
        Point2 secondStart,
        Point2 secondEnd,
        out double parameter)
    {
        var firstDeltaX = firstEnd.X - firstStart.X;
        var firstDeltaY = firstEnd.Y - firstStart.Y;
        var secondDeltaX = secondEnd.X - secondStart.X;
        var secondDeltaY = secondEnd.Y - secondStart.Y;
        var denominator = Cross(firstDeltaX, firstDeltaY, secondDeltaX, secondDeltaY);

        if (Math.Abs(denominator) <= 1e-12)
        {
            parameter = 0;
            return false;
        }

        var offsetX = secondStart.X - firstStart.X;
        var offsetY = secondStart.Y - firstStart.Y;
        var firstParameter = Cross(offsetX, offsetY, secondDeltaX, secondDeltaY) / denominator;
        var secondParameter = Cross(offsetX, offsetY, firstDeltaX, firstDeltaY) / denominator;
        if (firstParameter is < 0 or > 1 || secondParameter is < 0 or > 1)
        {
            parameter = 0;
            return false;
        }

        parameter = firstParameter;
        return true;
    }

    private static bool IsInsidePolygon(Point2 point, IReadOnlyList<Point2> polygon)
    {
        var inside = false;
        for (int currentIndex = 0, previousIndex = polygon.Count - 1;
             currentIndex < polygon.Count;
             previousIndex = currentIndex++)
        {
            var current = polygon[currentIndex];
            var previous = polygon[previousIndex];
            var crossesRay = (current.Y > point.Y) != (previous.Y > point.Y)
                && point.X < (previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y) + current.X;
            if (crossesRay)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static IEnumerable<(Point2 Start, Point2 End)> GetEdges(IReadOnlyList<Point2> polygon)
    {
        for (var index = 0; index < polygon.Count; index++)
        {
            yield return (polygon[index], polygon[(index + 1) % polygon.Count]);
        }
    }

    private static double DistanceToSegment(Point2 point, Point2 start, Point2 end)
    {
        var parameter = ProjectParameter(point, start, end);
        return Distance(point, Interpolate(start, end, parameter));
    }

    private static double ProjectParameter(Point2 point, Point2 start, Point2 end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared <= 1e-18)
        {
            return 0;
        }

        return Math.Clamp(((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared, 0, 1);
    }

    private static Point2 Interpolate(Point2 start, Point2 end, double parameter)
    {
        return new Point2(start.X + (end.X - start.X) * parameter, start.Y + (end.Y - start.Y) * parameter);
    }

    private static double Distance(Point2 first, Point2 second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static double Cross(double firstX, double firstY, double secondX, double secondY)
    {
        return firstX * secondY - firstY * secondX;
    }
}