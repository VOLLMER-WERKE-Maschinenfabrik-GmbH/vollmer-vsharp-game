using VGame.Contracts;
using VGame.Domain;

namespace VGame.Domain.Tests;

[TestClass]
[TestCategory("Domain")]
public sealed class HitZoneClassifierTests
{
    [TestMethod]
    [DataRow(90)]
    [DataRow(-90)]
    [DataRow(45)]
    public void FindFirstContact_RotationMatchesSixteenByNineDisplay(double degrees)
    {
        // Prüft die sichtbare Schneidenposition bei Drehung auf der nicht quadratischen 1280×720-Spielfläche.
        var definition = CreateTool("rotated-display", 0,
            Rectangle(0.2, 0.2, 0.5, 0.8, "shaft", HitZoneRole.Shaft),
            Rectangle(0.5, 0.2, 0.9, 0.8, "cut", HitZoneRole.Cut)) with { BoundingWidthNormalized = 0.2, BoundingHeightNormalized = 0.04 };
        var angle = degrees * Math.PI / 180;
        var localHorizontal = (0.8 - 0.5) * definition.BoundingWidthNormalized;
        var point = new Point2(0.5 + localHorizontal * Math.Cos(angle), 0.6 + localHorizontal * Math.Sin(angle) * 16 / 9);
        var placement = new ToolPlacement(new Point2(0.5, 0.6), 0, 0, degrees);
        var contact = HitZoneClassifier.FindFirstContact([new ToolSnapshot(definition, ToolState.Dull, placement)], [point], 0);
        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Cut, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_SwipeStartsInSmallCut_IsNotStolenByShaftTolerance()
    {
        // Prüft einen bereits in der Säge-Schneide begonnenen Swipe trotz toleranznahen Blattkörpers.
        var definition = CreateTool("small-saw", 0,
            Rectangle(0.1, 0.1, 0.9, 0.84, "shaft", HitZoneRole.Shaft),
            Rectangle(0.1, 0.84, 0.9, 0.95, "cut", HitZoneRole.Cut)) with { BoundingWidthNormalized = 0.12, BoundingHeightNormalized = 0.214 };
        var placement = new ToolPlacement(new Point2(0.5, 0.5), 0, 0, 0);
        var vertical = 0.5 + (0.9 - 0.5) * definition.BoundingHeightNormalized;
        var contact = HitZoneClassifier.FindFirstContact([new ToolSnapshot(definition, ToolState.Dull, placement)],
            [new Point2(0.5 + (0.3 - 0.5) * definition.BoundingWidthNormalized, vertical),
                new Point2(0.5 + (0.7 - 0.5) * definition.BoundingWidthNormalized, vertical)], 0.02);
        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Cut, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_TapInsideSmallCut_IsNotStolenByShaftTolerance()
    {
        // Prüft einen tatsächlichen Schneidentreffer bei Prototypgröße trotz näher als 0,02 liegendem Schaft.
        var definition = new ToolDefinition("small-drill", 0,
            [Rectangle(0.0625, 0.3, 0.832, 0.7, "shaft", HitZoneRole.Shaft),
                Rectangle(0.832, 0.35, 0.938, 0.7, "cut", HitZoneRole.Cut)],
            0, 0.2, 0.2 * 16 / 9 * 240 / 1024);
        var placement = new ToolPlacement(new Point2(0.5, 0.5), 0, 0, 0);
        var contact = HitZoneClassifier.FindFirstContact([new ToolSnapshot(definition, ToolState.Dull, placement)],
            [new Point2(0.5 + (0.9 - 0.5) * definition.BoundingWidthNormalized, 0.5)], 0.02);
        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Cut, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_SharedZoneBoundary_PrefersShaft()
    {
        // Prüft die verbindliche Vorrangregel an der gemeinsamen cut/shaft-Grenze.
        var tool = CreateTool(
            "tool-1",
            0,
            Rectangle(0.2, 0.2, 0.5, 0.8, "shaft", HitZoneRole.Shaft),
            Rectangle(0.5, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(tool, ToolState.Dull)],
            [new Point2(0.5, 0.5)],
            0);

        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Shaft, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_TapSharedBoundary_WithToleranceKeepsShaftPriority()
    {
        // Prüft, dass die bevorzugte direkte Tapwertung den verbindlichen Schaftvorrang an der Grenze nicht verändert.
        var definition = CreateTool("small-tool", 0,
            Rectangle(0.2, 0.2, 0.5, 0.8, "shaft", HitZoneRole.Shaft),
            Rectangle(0.5, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut)) with { BoundingWidthNormalized = 0.2, BoundingHeightNormalized = 0.08 };
        var contact = HitZoneClassifier.FindFirstContact([new ToolSnapshot(definition, ToolState.Dull, new(new(0.5, 0.5), 0, 0, 0))], [new Point2(0.5, 0.5)], 0.02);
        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Shaft, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_PathOrder_PrecedesVisualZOrder()
    {
        // Prüft, dass der erste Pfadkontakt vor einem späteren, visuell vorderen Werkzeug gewinnt.
        var earlierTool = CreateTool("earlier", 0, Rectangle(0.2, 0.2, 0.3, 0.8, "cut", HitZoneRole.Cut));
        var frontTool = CreateTool("front", 10, Rectangle(0.7, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(frontTool, ToolState.Dull), new ToolSnapshot(earlierTool, ToolState.Dull)],
            [new Point2(0.1, 0.5), new Point2(0.9, 0.5)],
            0);

        Assert.IsNotNull(contact);
        Assert.AreEqual("earlier", contact.ToolId);
    }

    [TestMethod]
    public void FindFirstContact_SamePathPosition_PrefersFrontmostTool()
    {
        // Prüft die Z-Reihenfolge als Gleichstandsregel für überlappende Werkzeuge.
        var rearTool = CreateTool("rear", 1, Rectangle(0.2, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));
        var frontTool = CreateTool("front", 2, Rectangle(0.2, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(rearTool, ToolState.Dull), new ToolSnapshot(frontTool, ToolState.Dull)],
            [new Point2(0.5, 0.5)],
            0);

        Assert.IsNotNull(contact);
        Assert.AreEqual("front", contact.ToolId);
    }

    [TestMethod]
    public void FindFirstContact_WithinTolerance_IsContact()
    {
        // Prüft die normalisierte Kontakttoleranz nahe außerhalb einer Zone.
        var tool = CreateTool("tool-1", 0, Rectangle(0.2, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(tool, ToolState.Dull)],
            [new Point2(0.19, 0.5)],
            0.02);

        Assert.IsNotNull(contact);
        Assert.AreEqual("tool-1", contact.ToolId);
    }

    [TestMethod]
    public void FindFirstContact_ResolvedTool_IsIgnored()
    {
        // Prüft, dass bereits geschärfte oder fehlgeschlagene Werkzeuge keine weitere Aktion auslösen.
        var tool = CreateTool("tool-1", 0, Rectangle(0.2, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut));
        var resolvedTools = new[]
        {
            new ToolSnapshot(tool, ToolState.Sharp),
            new ToolSnapshot(tool with { ToolId = "tool-2" }, ToolState.Failed)
        };

        var contact = HitZoneClassifier.FindFirstContact(resolvedTools, [new Point2(0.5, 0.5)], 0);

        Assert.IsNull(contact);
    }

    [TestMethod]
    public void FindFirstContact_PlacedTool_TransformsLocalZonesToCanvasCoordinates()
    {
        // Prüft, dass lokale Trefferzonen mit Größe und Position auf die Bühnenkoordinaten abgebildet werden.
        var definition = new ToolDefinition(
            "tool-1",
            0,
            [
                Rectangle(0.2, 0.2, 0.5, 0.8, "shaft", HitZoneRole.Shaft),
                Rectangle(0.5, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut)
            ],
            0,
            0.4,
            0.4);
        var placement = new ToolPlacement(new Point2(0.75, 0.6), 0, 0, 0);

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(definition, ToolState.Dull, placement)],
            [new Point2(0.8, 0.6)],
            0);

        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Cut, contact.HitZone.Role);
    }

    [TestMethod]
    public void FindFirstContact_RotatedTool_TransformsSharedBoundaryAndKeepsShaftPriority()
    {
        // Prüft, dass Rotation die gemeinsame Zonengrenze erhält und dort weiterhin der Schaft gewinnt.
        var definition = new ToolDefinition(
            "tool-1",
            0,
            [
                Rectangle(0.2, 0.2, 0.5, 0.8, "shaft", HitZoneRole.Shaft),
                Rectangle(0.5, 0.2, 0.8, 0.8, "cut", HitZoneRole.Cut)
            ],
            0,
            0.4,
            0.4);
        var placement = new ToolPlacement(new Point2(0.5, 0.5), 0, 0, 90);

        var contact = HitZoneClassifier.FindFirstContact(
            [new ToolSnapshot(definition, ToolState.Dull, placement)],
            [new Point2(0.5, 0.5)],
            0);

        Assert.IsNotNull(contact);
        Assert.AreEqual(HitZoneRole.Shaft, contact.HitZone.Role);
    }

    private static ToolDefinition CreateTool(string toolId, int zOrder, params HitZoneDefinition[] hitZones)
    {
        return new ToolDefinition(toolId, zOrder, hitZones, 0, 1, 1);
    }

    private static HitZoneDefinition Rectangle(double left, double top, double right, double bottom, string zoneId, HitZoneRole role)
    {
        return new HitZoneDefinition(zoneId, role,
        [
            new Point2(left, top),
            new Point2(right, top),
            new Point2(right, bottom),
            new Point2(left, bottom)
        ]);
    }
}