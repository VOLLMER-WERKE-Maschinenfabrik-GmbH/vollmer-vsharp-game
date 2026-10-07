using VGame.Domain;

namespace VGame.Domain.Tests;

[TestClass]
[TestCategory("Domain")]
public sealed class GameplayOptionsTests
{
    [TestMethod]
    public void Defaults_UseSixtySecondRounds()
    {
        // Prüft die verbindliche Standarddauer sowie das vollständige Startprofil.
        var options = new GameplayOptions();

        options.Validate();

        Assert.AreEqual(60, options.RoundDurationSeconds);
        Assert.AreEqual(10, options.CorrectScore);
        Assert.AreEqual(-5, options.WrongSharpeningPenalty);
        Assert.AreEqual(-10, options.ShaftHitPenalty);
        Assert.AreEqual(-5, options.MissPenalty);
    }

    [TestMethod]
    public void Validate_DurationOutsideConfiguredRange_ThrowsArgumentOutOfRangeException()
    {
        // Prüft beide Grenzen des konfigurierbaren Rundendauerbereichs.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { RoundDurationSeconds = 29 }.Validate());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { RoundDurationSeconds = 91 }.Validate());
    }

    [TestMethod]
    public void Validate_InvalidGestureTolerance_ThrowsArgumentOutOfRangeException()
    {
        // Prüft negative sowie nicht normalisierte Gestentoleranzen.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { ContactToleranceNormalized = -0.01 }.Validate());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { MinStrokeLengthNormalized = 1.01 }.Validate());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { MaxAngleDeviationDeg = 181 }.Validate());
    }

    [TestMethod]
    public void Validate_PositivePenalty_ThrowsArgumentOutOfRangeException()
    {
        // Verhindert, dass ein Fehlereignis versehentlich Punkte vergibt.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { MissPenalty = 1 }.Validate());
    }

    [TestMethod]
    public void Validate_UnsupportedVersion_ThrowsArgumentOutOfRangeException()
    {
        // Verhindert die Aktivierung eines unbekannten Konfigurationsschemas.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { Version = 2 }.Validate());
    }

    [TestMethod]
    public void Validate_StageAreaBelowMinimumArea_ThrowsArgumentOutOfRangeException()
    {
        // Erzwingt mindestens 55 Prozent detaillfreie Zielfläche gemäß Bühnenvertrag.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameplayOptions { StageAreaWidth = 0.8, StageAreaHeight = 0.6 }.Validate());
    }
}