namespace StructuraLens.Core.Analysis;

/// <summary>
/// Computes the Maintainability Index (MI) for a method.
/// 
/// Formula (SEI/Codacy variant, normalized to 0-100):
/// MI = max(0, 100 * (171 - 5.2*ln(V) - 0.23*CC - 16.2*ln(LOC)) / 171)
/// 
/// Where:
/// - V = Halstead Volume
/// - CC = Cyclomatic Complexity
/// - LOC = Lines of Executable Code
/// 
/// Interpretation:
/// - 0-9: Unmaintainable
/// - 10-19: Difficult to maintain
/// - 20-39: Moderate maintainability
/// - 40-100: Good maintainability
/// </summary>
public static class MaintainabilityIndexCalculator
{
    /// <summary>
    /// Calculates the Maintainability Index.
    /// </summary>
    /// <param name="halsteadVolume">Halstead Volume (V)</param>
    /// <param name="cyclomaticComplexity">Cyclomatic Complexity (CC)</param>
    /// <param name="linesOfCode">Lines of Executable Code (LOC)</param>
    /// <returns>MI value from 0 to 100</returns>
    public static double Calculate(double halsteadVolume, int cyclomaticComplexity, int linesOfCode)
    {
        // Handle edge cases to avoid log(0)
        if (halsteadVolume <= 0 || linesOfCode <= 0)
        {
            return 100.0; // Trivial code is perfectly maintainable
        }

        var lnVolume = Math.Log(halsteadVolume);
        var lnLoc = Math.Log(linesOfCode);

        var rawMi = 171 - (5.2 * lnVolume) - (0.23 * cyclomaticComplexity) - (16.2 * lnLoc);
        var normalizedMi = 100.0 * rawMi / 171.0;

        return Math.Max(0, Math.Min(100, normalizedMi));
    }
}
