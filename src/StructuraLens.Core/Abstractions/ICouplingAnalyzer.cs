using Microsoft.CodeAnalysis;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Analyzes coupling between projects, namespaces, and types.
/// </summary>
public interface ICouplingAnalyzer
{
    /// <summary>
    /// Analyzes coupling for an entire solution.
    /// </summary>
    /// <param name="solution">The Roslyn solution to analyze.</param>
    /// <param name="compilationCache">Optional cache of pre-compiled compilations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CouplingAnalysis> AnalyzeSolutionAsync(
        Solution solution,
        IReadOnlyDictionary<string, Compilation>? compilationCache = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes coupling for a single project.
    /// </summary>
    /// <param name="project">The Roslyn project to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CouplingAnalysis> AnalyzeProjectCouplingAsync(
        Project project,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds coupling analysis from pre-collected dependencies.
    /// Used when dependencies are collected during metrics analysis.
    /// </summary>
    /// <param name="solution">The Roslyn solution.</param>
    /// <param name="allDependencies">Pre-collected dependency edges.</param>
    CouplingAnalysis BuildCouplingAnalysisFromDependencies(
        Solution solution,
        IReadOnlyList<DependencyEdge> allDependencies);

    /// <summary>
    /// Builds coupling analysis from a dependency collector (streaming-friendly).
    /// Preferred method for large codebases as it works with aggregated edges.
    /// </summary>
    /// <param name="solution">The Roslyn solution.</param>
    /// <param name="collector">Dependency collector containing aggregated edges.</param>
    CouplingAnalysis BuildCouplingAnalysisFromCollector(
        Solution solution,
        IDependencyCollector collector);

    /// <summary>
    /// Analyzes internal coupling within a project.
    /// </summary>
    /// <param name="project">The Roslyn project to analyze.</param>
    /// <param name="compilationCache">Optional cache of pre-compiled compilations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing namespace coupling metrics and dependency edges.</returns>
    Task<(List<CouplingMetrics> namespaceCoupling, List<DependencyEdge> dependencies)> AnalyzeProjectInternalCouplingAsync(
        Project project,
        IReadOnlyDictionary<string, Compilation>? compilationCache = null,
        CancellationToken cancellationToken = default);
}
