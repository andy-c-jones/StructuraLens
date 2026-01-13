using Microsoft.CodeAnalysis;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Unified metrics calculation service wrapping all code metric calculators.
/// </summary>
public interface IMetricsCalculator
{
    /// <summary>
    /// Calculates cyclomatic complexity for a syntax node.
    /// </summary>
    int CalculateCyclomaticComplexity(SyntaxNode node);

    /// <summary>
    /// Calculates lines of executable code for a syntax node.
    /// </summary>
    int CalculateLinesOfCode(SyntaxNode node);

    /// <summary>
    /// Calculates Halstead metrics for a syntax node.
    /// </summary>
    /// <returns>Tuple containing (Operators, Operands, UniqueOperators, UniqueOperands, Volume, Difficulty, Effort)</returns>
    (int Operators, int Operands, int UniqueOperators, int UniqueOperands, double Volume, double Difficulty, double Effort) CalculateHalstead(SyntaxNode node);

    /// <summary>
    /// Calculates the maintainability index from component metrics.
    /// </summary>
    double CalculateMaintainabilityIndex(double halsteadVolume, int cyclomaticComplexity, int linesOfCode);

    /// <summary>
    /// Calculates depth of inheritance for a type symbol.
    /// </summary>
    int CalculateDepthOfInheritance(INamedTypeSymbol? typeSymbol);

    /// <summary>
    /// Calculates all metrics in a single pass for efficiency.
    /// </summary>
    /// <returns>Tuple containing (CyclomaticComplexity, LinesOfCode, HalsteadVolume, MaintainabilityIndex)</returns>
    (int CyclomaticComplexity, int LinesOfCode, double HalsteadVolume, double MaintainabilityIndex) CalculateUnifiedMetrics(SyntaxNode node);
}
