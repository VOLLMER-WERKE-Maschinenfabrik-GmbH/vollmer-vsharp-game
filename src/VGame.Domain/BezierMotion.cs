using VGame.Contracts;

namespace VGame.Domain;

public static class BezierMotion
{
    public static ToolPlacement Advance(ToolPlacement placement, double elapsedSeconds)
    {
        var curve = placement.Curve ?? throw new ArgumentException("A Bézier path is required.", nameof(placement));
        var time = (placement.CurveTimeSeconds + elapsedSeconds) % (curve.DurationSeconds * 2);
        var phase = Math.PI * time / curve.DurationSeconds;
        var progress = (1 - Math.Cos(phase)) / 2;
        var progressVelocity = Math.PI * Math.Sin(phase) / (2 * curve.DurationSeconds);
        var remaining = 1 - progress;
        var center = new Point2(
            remaining * remaining * remaining * curve.Start.X + 3 * remaining * remaining * progress * curve.FirstControl.X
                + 3 * remaining * progress * progress * curve.SecondControl.X + progress * progress * progress * curve.End.X,
            remaining * remaining * remaining * curve.Start.Y + 3 * remaining * remaining * progress * curve.FirstControl.Y
                + 3 * remaining * progress * progress * curve.SecondControl.Y + progress * progress * progress * curve.End.Y);
        var velocityHorizontal = 3 * remaining * remaining * (curve.FirstControl.X - curve.Start.X)
            + 6 * remaining * progress * (curve.SecondControl.X - curve.FirstControl.X) + 3 * progress * progress * (curve.End.X - curve.SecondControl.X);
        var velocityVertical = 3 * remaining * remaining * (curve.FirstControl.Y - curve.Start.Y)
            + 6 * remaining * progress * (curve.SecondControl.Y - curve.FirstControl.Y) + 3 * progress * progress * (curve.End.Y - curve.SecondControl.Y);
        return placement with { Center = center, VelocityXPerSecond = velocityHorizontal * progressVelocity,
            VelocityYPerSecond = velocityVertical * progressVelocity, CurveTimeSeconds = time };
    }
}