using Microsoft.CodeAnalysis;
using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Unified metrics calculation service that wraps all code metric calculators.
/// Provides a single injectable service for all metric calculations.
/// </summary>
public sealed class MetricsCalculator : IMetricsCalculator
{
    /// <inheritdoc />
    public int CalculateCyclomaticComplexity(SyntaxNode node)
        => CyclomaticComplexityCalculator.Calculate(node);

    /// <inheritdoc />
    public int CalculateLinesOfCode(SyntaxNode node)
        => LinesOfCodeCalculator.Calculate(node);

    /// <inheritdoc />
    public (int Operators, int Operands, int UniqueOperators, int UniqueOperands, double Volume, double Difficulty, double Effort) CalculateHalstead(SyntaxNode node)
    {
        var metrics = HalsteadCalculator.Calculate(node);
        return (
            metrics.TotalOperators,
            metrics.TotalOperands,
            metrics.DistinctOperators,
            metrics.DistinctOperands,
            metrics.Volume,
            metrics.Difficulty,
            metrics.Effort
        );
    }

    /// <inheritdoc />
    public double CalculateMaintainabilityIndex(double halsteadVolume, int cyclomaticComplexity, int linesOfCode)
        => MaintainabilityIndexCalculator.Calculate(halsteadVolume, cyclomaticComplexity, linesOfCode);

    /// <inheritdoc />
    public int CalculateDepthOfInheritance(INamedTypeSymbol? typeSymbol)
        => DepthOfInheritanceCalculator.Calculate(typeSymbol);

    /// <inheritdoc />
    public (int CyclomaticComplexity, int LinesOfCode, double HalsteadVolume, double MaintainabilityIndex) CalculateUnifiedMetrics(SyntaxNode node)
    {
        var metrics = UnifiedMetricsCalculator.Calculate(node);
        return (
            metrics.CyclomaticComplexity,
            metrics.LinesOfCode,
            metrics.HalsteadVolume,
            metrics.MaintainabilityIndex
        );
    }
}
