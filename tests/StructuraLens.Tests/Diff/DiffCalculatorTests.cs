using StructuraLens.Core.Diff;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Diff;

public sealed class DiffCalculatorTests
{
    [Test]
    public async Task Compare_DiagnosticMovedWithinSameFile_ReportsMovedNotAddedOrResolved()
    {
        var calculator = new DiffCalculator();
        var baseReport = CreateReport(Diagnostic("NSDEPCOP01", DiagnosticLevel.Warning, "Illegal namespace reference", "Controller.cs", 289, 26));
        var headReport = CreateReport(Diagnostic("NSDEPCOP01", DiagnosticLevel.Warning, "Illegal namespace reference", "Controller.cs", 291, 26));

        var diff = calculator.Compare(baseReport, headReport);

        await Assert.That(diff.Diagnostics.NewWarnings).IsEqualTo(0);
        await Assert.That(diff.Diagnostics.ResolvedWarnings).IsEqualTo(0);
        await Assert.That(diff.Diagnostics.MovedWarnings).IsEqualTo(1);
        await Assert.That(diff.Diagnostics.AddedDiagnostics).IsEmpty();
        await Assert.That(diff.Diagnostics.ResolvedDiagnostics).IsEmpty();

        var moved = diff.Diagnostics.MovedDiagnostics.Single();
        await Assert.That(moved.Id).IsEqualTo("NSDEPCOP01");
        await Assert.That(moved.File).IsEqualTo("Controller.cs");
        await Assert.That(moved.BaseLine).IsEqualTo(289);
        await Assert.That(moved.BaseColumn).IsEqualTo(26);
        await Assert.That(moved.HeadLine).IsEqualTo(291);
        await Assert.That(moved.HeadColumn).IsEqualTo(26);
    }

    [Test]
    public async Task Compare_DuplicateDiagnosticsMovedAndAdded_KeepsOnlyTrueAddition()
    {
        var calculator = new DiffCalculator();
        var baseReport = CreateReport(
            Diagnostic("NUnit2056", DiagnosticLevel.Info, "Consider using Assert.EnterMultipleScope", "Tests.cs", 91, 9),
            Diagnostic("NUnit2056", DiagnosticLevel.Info, "Consider using Assert.EnterMultipleScope", "Tests.cs", 154, 9));
        var headReport = CreateReport(
            Diagnostic("NUnit2056", DiagnosticLevel.Info, "Consider using Assert.EnterMultipleScope", "Tests.cs", 94, 9),
            Diagnostic("NUnit2056", DiagnosticLevel.Info, "Consider using Assert.EnterMultipleScope", "Tests.cs", 154, 9),
            Diagnostic("NUnit2056", DiagnosticLevel.Info, "Consider using Assert.EnterMultipleScope", "Tests.cs", 276, 9));

        var diff = calculator.Compare(baseReport, headReport);

        await Assert.That(diff.Diagnostics.NewInfo).IsEqualTo(1);
        await Assert.That(diff.Diagnostics.ResolvedInfo).IsEqualTo(0);
        await Assert.That(diff.Diagnostics.MovedInfo).IsEqualTo(1);
        await Assert.That(diff.Diagnostics.AddedDiagnostics.Count).IsEqualTo(1);
        await Assert.That(diff.Diagnostics.AddedDiagnostics[0].Line).IsEqualTo(276);

        var moved = diff.Diagnostics.MovedDiagnostics.Single();
        await Assert.That(moved.BaseLine).IsEqualTo(91);
        await Assert.That(moved.HeadLine).IsEqualTo(94);
    }

    private static AnalysisReport CreateReport(params DiagnosticInfo[] diagnostics)
    {
        var summary = new DiagnosticSummary
        {
            ErrorCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Error),
            WarningCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Warning),
            InfoCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Info),
            HiddenCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Hidden),
            Diagnostics = diagnostics
        };

        var project = new ProjectMetrics("TestProject", "TestProject.csproj", [])
        {
            Diagnostics = summary
        };

        return new AnalysisReport("Test.sln", DateTime.UtcNow, [project], [], "test");
    }

    private static DiagnosticInfo Diagnostic(string id, DiagnosticLevel severity, string message, string file, int line, int column) =>
        new(id, message, severity, file, line, column);
}
