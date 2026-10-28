using VGame.Contracts;

namespace VGame.Domain;

public static class ToolGeometry
{
    public const double CanvasAspectRatio = 16d / 9;

    public static bool RotationEnvelopesOverlap(double width, double height, double otherWidth, double otherHeight,
        double deltaX, double deltaY, double spacingFactor)
    {
        var minimumDistance = spacingFactor * (HalfExtents(width, height, 0, true).X + HalfExtents(otherWidth, otherHeight, 0, true).X);
        var verticalDistance = deltaY / CanvasAspectRatio;
        return deltaX * deltaX + verticalDistance * verticalDistance < minimumDistance * minimumDistance;
    }

    public static Point2 HalfExtents(double width, double height, double rotationDegrees, bool fullRotation)
    {
        if (fullRotation)
        {
            var radius = Math.Sqrt(width * width + height * height / (CanvasAspectRatio * CanvasAspectRatio)) / 2;
            return new(radius, radius * CanvasAspectRatio);
        }
        var angle = rotationDegrees * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(angle));
        var sine = Math.Abs(Math.Sin(angle));
        return new((cosine * width + sine * height / CanvasAspectRatio) / 2,
            (sine * width * CanvasAspectRatio + cosine * height) / 2);
    }
}