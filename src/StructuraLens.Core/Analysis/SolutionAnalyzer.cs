using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Main analyzer that loads a solution and computes metrics for all projects.
/// </summary>
public sealed class SolutionAnalyzer
{
    private static bool _msBuildRegistered;
    private static readonly object _lock = new();

    private readonly List<string> _warnings = [];

    public static void EnsureMSBuildRegistered()
    {
        if (_msBuildRegistered) return;

        lock (_lock)
        {
            if (_msBuildRegistered) return;

            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count > 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(i => i.Version).First());
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
            }
            _msBuildRegistered = true;
        }
    }

    public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Solution file not found: {fullPath}");
        }

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
        });

        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        var projectMetricsList = new List<ProjectMetrics>();

        foreach (var project in solution.Projects)
        {
            if (project.Language != LanguageNames.CSharp)
                continue;

            var projectMetrics = await AnalyzeProjectAsync(project, cancellationToken);
            projectMetricsList.Add(projectMetrics);
        }

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList,
            Warnings: _warnings);
    }

    public async Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Project file not found: {fullPath}");
        }

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
        });

        var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
        var projectMetrics = await AnalyzeProjectAsync(project, cancellationToken);

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings);
    }

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            _warnings.Add($"Could not get compilation for project: {project.Name}");
            return new ProjectMetrics(project.Name, project.FilePath ?? "", []);
        }

        var typeMetricsList = new List<TypeMetrics>();

        foreach (var document in project.Documents)
        {
            if (document.SourceCodeKind != SourceCodeKind.Regular)
                continue;

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            if (syntaxTree == null || semanticModel == null)
                continue;

            var root = await syntaxTree.GetRootAsync(cancellationToken);
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                var dit = DepthOfInheritanceCalculator.Calculate(typeSymbol);

                var methodMetricsList = new List<MethodMetrics>();

                var methods = typeDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>();

                foreach (var method in methods)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                    var fullName = methodSymbol?.ToDisplayString() ?? method.Identifier.Text;

                    var cc = method.Body != null || method.ExpressionBody != null
                        ? CyclomaticComplexityCalculator.Calculate(method)
                        : 1;

                    var loc = method.Body != null
                        ? LinesOfCodeCalculator.Calculate(method.Body)
                        : (method.ExpressionBody != null ? 1 : 0);

                    var halstead = HalsteadCalculator.Calculate(method);
                    var mi = MaintainabilityIndexCalculator.Calculate(halstead.Volume, cc, loc);

                    var lineSpan = method.GetLocation().GetLineSpan();

                    methodMetricsList.Add(new MethodMetrics(
                        FullName: fullName,
                        FilePath: document.FilePath ?? "",
                        StartLine: lineSpan.StartLinePosition.Line + 1,
                        EndLine: lineSpan.EndLinePosition.Line + 1,
                        CyclomaticComplexity: cc,
                        LinesOfExecutableCode: loc,
                        HalsteadVolume: halstead.Volume,
                        MaintainabilityIndex: mi));
                }

                // Also analyze constructors, properties, etc.
                var constructors = typeDecl.DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>();

                foreach (var ctor in constructors)
                {
                    var ctorSymbol = semanticModel.GetDeclaredSymbol(ctor);
                    var fullName = ctorSymbol?.ToDisplayString() ?? $"{typeDecl.Identifier.Text}.ctor";

                    var cc = ctor.Body != null || ctor.ExpressionBody != null
                        ? CyclomaticComplexityCalculator.Calculate(ctor)
                        : 1;

                    var loc = ctor.Body != null
                        ? LinesOfCodeCalculator.Calculate(ctor.Body)
                        : (ctor.ExpressionBody != null ? 1 : 0);

                    var halstead = HalsteadCalculator.Calculate(ctor);
                    var mi = MaintainabilityIndexCalculator.Calculate(halstead.Volume, cc, loc);

                    var lineSpan = ctor.GetLocation().GetLineSpan();

                    methodMetricsList.Add(new MethodMetrics(
                        FullName: fullName,
                        FilePath: document.FilePath ?? "",
                        StartLine: lineSpan.StartLinePosition.Line + 1,
                        EndLine: lineSpan.EndLinePosition.Line + 1,
                        CyclomaticComplexity: cc,
                        LinesOfExecutableCode: loc,
                        HalsteadVolume: halstead.Volume,
                        MaintainabilityIndex: mi));
                }

                var lineSpanType = typeDecl.GetLocation().GetLineSpan();
                var typeName = typeSymbol?.ToDisplayString() ?? typeDecl.Identifier.Text;

                typeMetricsList.Add(new TypeMetrics(
                    FullName: typeName,
                    FilePath: document.FilePath ?? "",
                    DepthOfInheritance: dit,
                    Methods: methodMetricsList));
            }
        }

        return new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: typeMetricsList);
    }
}
