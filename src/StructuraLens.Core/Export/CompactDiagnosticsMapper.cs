using StructuraLens.Core.Models;

namespace StructuraLens.Core.Export;

internal static class CompactDiagnosticsMapper
{
    public static CompactDiagnostics? Export(AnalysisReport report)
    {
        var allDiagnostics = report.Projects
            .Where(p => p.Diagnostics != null)
            .SelectMany(p => p.Diagnostics!.Diagnostics.Select(d => new { Project = p.Name, Diagnostic = d }))
            .ToList();

        if (allDiagnostics.Count == 0)
        {
            return null;
        }

        var items = allDiagnostics.Select(x => new object[]
        {
            x.Project,
            x.Diagnostic.Id,
            GetSeverityCode(x.Diagnostic.Severity),
            x.Diagnostic.Message,
            x.Diagnostic.FilePath,
            x.Diagnostic.Line,
            x.Diagnostic.Column
        }).ToList();

        return new CompactDiagnostics
        {
            Errors = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Error),
            Warnings = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Warning),
            Info = allDiagnostics.Count(x => x.Diagnostic.Severity == DiagnosticLevel.Info),
            Items = items
        };
    }

    private static int GetSeverityCode(DiagnosticLevel severity)
    {
        return severity switch
        {
            DiagnosticLevel.Error => 3,
            DiagnosticLevel.Warning => 2,
            DiagnosticLevel.Info => 1,
            _ => 0
        };
    }
}
