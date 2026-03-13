using Microsoft.Extensions.Logging;

namespace StructuraLens.Core.Analysis.Logging;

/// <summary>
/// Source-generated high-performance logging methods for CouplingAnalyzer.
/// </summary>
internal static partial class CouplingAnalyzerLog
{
    // Analysis events (2000-2099)

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Analyzing coupling in project {index}/{total}: {projectName}")]
    public static partial void AnalyzingCouplingInProject(ILogger logger, int index, int total, string projectName);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Project {projectName}: {dependencyCount} dependencies found")]
    public static partial void ProjectDependenciesFound(ILogger logger, string projectName, int dependencyCount);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Analyzing {documentCount} documents for coupling in {projectName}")]
    public static partial void AnalyzingDocumentsForCoupling(ILogger logger, int documentCount, string projectName);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Coupling analysis progress: {documentIndex}/{documentCount} documents in {projectName}")]
    public static partial void CouplingAnalysisProgress(ILogger logger, int documentIndex, int documentCount, string projectName);

    // Warning events (2100-2199)

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "Could not get compilation for project: {projectName}")]
    public static partial void CouldNotGetCompilation(ILogger logger, string projectName);
}
