using Microsoft.Extensions.Logging;

namespace StructuraLens.Core.Analysis.Logging;

/// <summary>
/// Source-generated high-performance logging methods for SolutionAnalyzer.
/// </summary>
internal static partial class SolutionAnalyzerLog
{
    // Analysis lifecycle events (1000-1099)
    
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Starting solution analysis: {solutionPath}")]
    public static partial void StartingSolutionAnalysis(ILogger logger, string solutionPath);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Starting project analysis: {projectPath}")]
    public static partial void StartingProjectAnalysis(ILogger logger, string projectPath);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Restoring NuGet packages...")]
    public static partial void RestoringNuGetPackages(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Loading solution into MSBuild workspace...")]
    public static partial void LoadingSolutionIntoWorkspace(ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Loading project into MSBuild workspace...")]
    public static partial void LoadingProjectIntoWorkspace(ILogger logger);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Loaded solution with {projectCount} C# projects")]
    public static partial void LoadedSolutionWithProjects(ILogger logger, int projectCount);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Pre-fetching compilations for all projects...")]
    public static partial void PreFetchingCompilations(ILogger logger);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Cached {count} compilations")]
    public static partial void CachedCompilations(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Analyzing project {index}/{total}: {projectName}")]
    public static partial void AnalyzingProject(ILogger logger, int index, int total, string projectName);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Completed {projectName}: {typeCount} types, {methodCount} methods")]
    public static partial void CompletedProject(ILogger logger, string projectName, int typeCount, int methodCount);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Building coupling analysis from {depCount} dependencies")]
    public static partial void BuildingCouplingAnalysis(ILogger logger, int depCount);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Analysis complete. Total: {projectCount} projects, {typeCount} types, {methodCount} methods")]
    public static partial void AnalysisComplete(ILogger logger, int projectCount, int typeCount, int methodCount);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Analyzing project: {projectName}")]
    public static partial void AnalyzingProjectSingle(ILogger logger, string projectName);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Information,
        Message = "Analyzing project coupling")]
    public static partial void AnalyzingProjectCoupling(ILogger logger);

    // Debug events (1050-1099)
    
    [LoggerMessage(
        EventId = 1050,
        Level = LogLevel.Debug,
        Message = "Getting compilation for project: {projectName}")]
    public static partial void GettingCompilationForProject(ILogger logger, string projectName);

    [LoggerMessage(
        EventId = 1051,
        Level = LogLevel.Debug,
        Message = "Analyzing {documentCount} documents in project {projectName}")]
    public static partial void AnalyzingDocumentsInProject(ILogger logger, int documentCount, string projectName);

    [LoggerMessage(
        EventId = 1052,
        Level = LogLevel.Debug,
        Message = "Progress: {documentIndex}/{documentCount} documents processed in {projectName}")]
    public static partial void DocumentProcessingProgress(ILogger logger, int documentIndex, int documentCount, string projectName);

    // Warning events (1100-1199)
    
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Workspace warning: {message}")]
    public static partial void WorkspaceWarning(ILogger logger, string message);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Could not get compilation for project: {projectName}")]
    public static partial void CouldNotGetCompilation(ILogger logger, string projectName);
}
