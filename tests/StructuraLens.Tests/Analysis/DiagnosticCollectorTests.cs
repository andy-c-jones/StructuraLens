#pragma warning disable RS1036
#pragma warning disable RS1038
#pragma warning disable RS1041

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class DiagnosticCollectorTests
{
    [Test]
    public async Task CollectAsync_WithoutSuppressor_IncludesCompilerWarning()
    {
        var project = CreateProject(withSuppressor: false);
        var compilation = await project.GetCompilationAsync();

        await Assert.That(compilation).IsNotNull();

        var summary = await DiagnosticCollector.CollectAsync(
            project,
            compilation!,
            concurrentAnalyzerExecution: true,
            cancellationToken: CancellationToken.None);

        var hasCs0168 = summary.Diagnostics.Any(d => d.Id == "CS0168");
        await Assert.That(hasCs0168).IsTrue();
    }

    [Test]
    public async Task CollectAsync_WithDiagnosticSuppressor_ExcludesSuppressedCompilerWarning()
    {
        var project = CreateProject(withSuppressor: true);
        var compilation = await project.GetCompilationAsync();

        await Assert.That(compilation).IsNotNull();

        var summary = await DiagnosticCollector.CollectAsync(
            project,
            compilation!,
            concurrentAnalyzerExecution: true,
            cancellationToken: CancellationToken.None);

        var hasCs0168 = summary.Diagnostics.Any(d => d.Id == "CS0168");
        await Assert.That(hasCs0168).IsFalse();
    }

    [Test]
    public async Task CollectAsync_GeneratedObjSource_ExcludesDiagnostics()
    {
        var project = CreateProjectWithGeneratedWarning();
        var compilation = await project.GetCompilationAsync();

        await Assert.That(compilation).IsNotNull();

        var summary = await DiagnosticCollector.CollectAsync(
            project,
            compilation!,
            concurrentAnalyzerExecution: true,
            cancellationToken: CancellationToken.None);

        await Assert.That(summary.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task CollectAsync_UserAssemblyInfoSource_IncludesDiagnostics()
    {
        var project = CreateProjectWithUserAssemblyInfoWarning();
        var compilation = await project.GetCompilationAsync();

        await Assert.That(compilation).IsNotNull();

        var summary = await DiagnosticCollector.CollectAsync(
            project,
            compilation!,
            concurrentAnalyzerExecution: true,
            cancellationToken: CancellationToken.None);

        await Assert.That(summary.Diagnostics.Any(d => d.Id == "CS0168")).IsTrue();
    }

    private static Project CreateProject(bool withSuppressor)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, warningLevel: 4),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var project = workspace
            .AddProject(projectInfo)
            .AddDocument("WarningFixture.cs", """
                public sealed class WarningFixture
                {
                    public void Execute()
                    {
                        int unused;
                    }
                }
                """)
            .Project;

        if (!withSuppressor)
        {
            return project;
        }

        var analyzerReference = new AnalyzerFileReference(
            typeof(Cs0168Suppressor).Assembly.Location,
            new TestAnalyzerAssemblyLoader());

        return project.AddAnalyzerReference(analyzerReference);
    }

    private static Project CreateProjectWithGeneratedWarning()
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "GeneratedDiagnosticsProject",
            "GeneratedDiagnosticsProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, warningLevel: 4),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var generatedFilePath = Path.Combine(
            Path.GetTempPath(),
            "StructuraLensTests",
            "obj",
            "Debug",
            "net10.0",
            "GeneratedWarning.cs");

        return workspace
            .AddProject(projectInfo)
            .AddDocument(
                "GeneratedWarning.cs",
                SourceText.From(
                    """
                    public sealed class GeneratedWarning
                    {
                        public void Execute()
                        {
                            int unused;
                        }
                    }
                    """),
                filePath: generatedFilePath)
            .Project;
    }

    private static Project CreateProjectWithUserAssemblyInfoWarning()
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "UserAssemblyInfoDiagnosticsProject",
            "UserAssemblyInfoDiagnosticsProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, warningLevel: 4),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var sourceFilePath = Path.Combine(
            Path.GetTempPath(),
            "StructuraLensTests",
            "Properties",
            "AssemblyInfo.cs");

        return workspace
            .AddProject(projectInfo)
            .AddDocument(
                "AssemblyInfo.cs",
                SourceText.From(
                    """
                    public sealed class UserAssemblyInfoWarning
                    {
                        public void Execute()
                        {
                            int unused;
                        }
                    }
                    """),
                filePath: sourceFilePath)
            .Project;
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class Cs0168Suppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor Descriptor =
            new("TESTSUP0001", "CS0168", "Suppress CS0168 for test coverage.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [Descriptor];

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics.Where(d => d.Id == "CS0168"))
            {
                context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
            }
        }
    }

    private sealed class TestAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public System.Reflection.Assembly LoadFromPath(string fullPath)
        {
            return System.Reflection.Assembly.LoadFrom(fullPath);
        }
    }
}
