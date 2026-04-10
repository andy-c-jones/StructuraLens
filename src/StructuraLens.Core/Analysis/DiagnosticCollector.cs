using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

internal static class DiagnosticCollector
{
    public static async Task<DiagnosticSummary> CollectAsync(
        Project project,
        Compilation compilation,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
    {
        var diagnostics = await GetDiagnosticsAsync(project, compilation, cancellationToken, concurrentAnalyzerExecution);

        var filteredDiagnostics = diagnostics
            .Where(ShouldIncludeDiagnostic)
            .Select(d => new DiagnosticInfo(
                Id: d.Id,
                Message: d.GetMessage(),
                Severity: MapSeverity(d.Severity),
                FilePath: d.Location.SourceTree?.FilePath ?? string.Empty,
                Line: d.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column: d.Location.GetLineSpan().StartLinePosition.Character + 1)
            {
                Category = d.Descriptor.Category,
                HelpLink = d.Descriptor.HelpLinkUri
            })
            .ToList();

        return new DiagnosticSummary
        {
            ErrorCount = filteredDiagnostics.Count(d => d.Severity == DiagnosticLevel.Error),
            WarningCount = filteredDiagnostics.Count(d => d.Severity == DiagnosticLevel.Warning),
            InfoCount = filteredDiagnostics.Count(d => d.Severity == DiagnosticLevel.Info),
            HiddenCount = filteredDiagnostics.Count(d => d.Severity == DiagnosticLevel.Hidden),
            Diagnostics = filteredDiagnostics
        };
    }

    private static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(
        Project project,
        Compilation compilation,
        CancellationToken cancellationToken,
        bool concurrentAnalyzerExecution)
    {
        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .Distinct()
            .ToImmutableArray();

        if (analyzers.Length == 0)
        {
            return compilation.GetDiagnostics(cancellationToken);
        }

        var options = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: concurrentAnalyzerExecution,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, options);
        return await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
    }

    private static bool ShouldIncludeDiagnostic(Diagnostic diagnostic)
    {
        return !diagnostic.IsSuppressed &&
               (diagnostic.Severity != DiagnosticSeverity.Hidden ||
                diagnostic.Id.StartsWith("CS", StringComparison.OrdinalIgnoreCase));
    }

    private static DiagnosticLevel MapSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => DiagnosticLevel.Error,
            DiagnosticSeverity.Warning => DiagnosticLevel.Warning,
            DiagnosticSeverity.Info => DiagnosticLevel.Info,
            _ => DiagnosticLevel.Hidden
        };
    }
}
