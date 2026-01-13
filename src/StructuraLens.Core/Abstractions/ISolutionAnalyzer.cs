using StructuraLens.Core.Models;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Main analyzer interface for analyzing solutions and projects.
/// </summary>
public interface ISolutionAnalyzer
{
    /// <summary>
    /// Analyzes a solution file and returns a comprehensive report.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln or .slnx).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis report containing all metrics and coupling information.</returns>
    Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a project file and returns a comprehensive report.
    /// </summary>
    /// <param name="projectPath">Path to the project file (.csproj).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis report containing all metrics and coupling information.</returns>
    Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, CancellationToken cancellationToken = default);
}
