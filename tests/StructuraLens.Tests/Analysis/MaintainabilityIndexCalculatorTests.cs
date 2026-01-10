using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class MaintainabilityIndexCalculatorTests
{
    [Test]
    public async Task Calculate_ZeroVolume_ReturnsMaxMaintainability()
    {
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 0,
            cyclomaticComplexity: 1,
            linesOfCode: 1);

        await Assert.That(mi).IsEqualTo(100.0);
    }

    [Test]
    public async Task Calculate_ZeroLOC_ReturnsMaxMaintainability()
    {
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100,
            cyclomaticComplexity: 1,
            linesOfCode: 0);

        await Assert.That(mi).IsEqualTo(100.0);
    }

    [Test]
    public async Task Calculate_SimpleMethod_ReturnsHighMaintainability()
    {
        // Simple method: low volume, low complexity, few lines
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 20,
            cyclomaticComplexity: 1,
            linesOfCode: 3);

        // Should be above 40 (good maintainability)
        await Assert.That(mi).IsGreaterThan(40);
    }

    [Test]
    public async Task Calculate_ComplexMethod_ReturnsLowerMaintainability()
    {
        // Complex method: high volume, high complexity, many lines
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 5000,
            cyclomaticComplexity: 50,
            linesOfCode: 200);

        // Should be below 40
        await Assert.That(mi).IsLessThan(40);
    }

    [Test]
    public async Task Calculate_VeryComplexMethod_ReturnsLowMaintainability()
    {
        // Very complex method
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 20000,
            cyclomaticComplexity: 100,
            linesOfCode: 500);

        // Should be below 20 (difficult to maintain)
        await Assert.That(mi).IsLessThan(20);
    }

    [Test]
    public async Task Calculate_NeverReturnsNegative()
    {
        // Extremely complex values that would give negative raw MI
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100000,
            cyclomaticComplexity: 500,
            linesOfCode: 10000);

        await Assert.That(mi).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Calculate_NeverExceeds100()
    {
        // Very simple method
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 1,
            cyclomaticComplexity: 1,
            linesOfCode: 1);

        await Assert.That(mi).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task Calculate_IncreasingVolume_DecreasesMI()
    {
        var mi1 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 50,
            cyclomaticComplexity: 5,
            linesOfCode: 10);

        var mi2 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 500,
            cyclomaticComplexity: 5,
            linesOfCode: 10);

        await Assert.That(mi2).IsLessThan(mi1);
    }

    [Test]
    public async Task Calculate_IncreasingComplexity_DecreasesMI()
    {
        var mi1 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100,
            cyclomaticComplexity: 2,
            linesOfCode: 10);

        var mi2 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100,
            cyclomaticComplexity: 20,
            linesOfCode: 10);

        await Assert.That(mi2).IsLessThan(mi1);
    }

    [Test]
    public async Task Calculate_IncreasingLOC_DecreasesMI()
    {
        var mi1 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100,
            cyclomaticComplexity: 5,
            linesOfCode: 5);

        var mi2 = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 100,
            cyclomaticComplexity: 5,
            linesOfCode: 100);

        await Assert.That(mi2).IsLessThan(mi1);
    }

    [Test]
    public async Task Calculate_TypicalValues_ProducesExpectedRange()
    {
        // Typical method: moderate metrics
        var mi = MaintainabilityIndexCalculator.Calculate(
            halsteadVolume: 200,
            cyclomaticComplexity: 10,
            linesOfCode: 25);

        // Should be in the "moderate" range (20-40) or good range
        await Assert.That(mi).IsGreaterThan(20);
        await Assert.That(mi).IsLessThan(100);
    }

    [Test]
    public async Task Calculate_FormulaCorrectness_MatchesExpectedValue()
    {
        // Test with known values to verify the formula
        // MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)
        double volume = 100;
        int cc = 5;
        int loc = 10;

        double expectedRaw = 171 - (5.2 * Math.Log(volume)) - (0.23 * cc) - (16.2 * Math.Log(loc));
        double expectedNormalized = 100.0 * expectedRaw / 171.0;
        double expected = Math.Max(0, Math.Min(100, expectedNormalized));

        var actual = MaintainabilityIndexCalculator.Calculate(volume, cc, loc);

        await Assert.That(Math.Abs(actual - expected)).IsLessThan(0.001);
    }
}
