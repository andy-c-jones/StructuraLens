using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using StructuraLens.Core.Configuration;
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

    /// <summary>
    /// Analyzes a solution using default configuration.
    /// </summary>
    public Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return AnalyzeSolutionAsync(solutionPath, ConfigurationLoader.CreateDefaultConfig(), cancellationToken);
    }

    /// <summary>
    /// Analyzes a solution with specified configuration.
    /// </summary>
    public async Task<AnalysisReport> AnalyzeSolutionAsync(string solutionPath, StructuraLensConfig config, CancellationToken cancellationToken = default)
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

        // Analyze coupling across the entire solution with configuration
        var couplingAnalysis = await CouplingAnalyzer.AnalyzeSolutionAsync(solution, config, cancellationToken);

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList,
            Warnings: _warnings)
        {
            CouplingAnalysis = couplingAnalysis
        };
    }

    /// <summary>
    /// Analyzes a project using default configuration.
    /// </summary>
    public Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        return AnalyzeProjectAsync(projectPath, ConfigurationLoader.CreateDefaultConfig(), cancellationToken);
    }

    /// <summary>
    /// Analyzes a project with specified configuration.
    /// </summary>
    public async Task<AnalysisReport> AnalyzeProjectAsync(string projectPath, StructuraLensConfig config, CancellationToken cancellationToken = default)
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

        // Analyze internal coupling within the project with configuration
        var couplingAnalysis = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, config, cancellationToken);

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings)
        {
            CouplingAnalysis = couplingAnalysis
        };
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
            
            // Analyze traditional type declarations
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeMetrics = AnalyzeTypeDeclaration(typeDecl, semanticModel, document.FilePath ?? "");
                typeMetricsList.Add(typeMetrics);
            }

            // Analyze top-level statements (C# 9+ feature)
            var topLevelStatements = root.DescendantNodes()
                .OfType<GlobalStatementSyntax>()
                .ToList();

            if (topLevelStatements.Count > 0)
            {
                var topLevelMetrics = AnalyzeTopLevelStatements(root, topLevelStatements, semanticModel, document.FilePath ?? "");
                if (topLevelMetrics != null)
                {
                    typeMetricsList.Add(topLevelMetrics);
                }
            }
        }

        return new ProjectMetrics(
            Name: project.Name,
            FilePath: project.FilePath ?? "",
            Types: typeMetricsList);
    }

    private TypeMetrics AnalyzeTypeDeclaration(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        var dit = DepthOfInheritanceCalculator.Calculate(typeSymbol);

        var methodMetricsList = new List<MethodMetrics>();

        var methods = typeDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var metrics = AnalyzeMethod(method, semanticModel, filePath);
            methodMetricsList.Add(metrics);
        }

        var constructors = typeDecl.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>();

        foreach (var ctor in constructors)
        {
            var metrics = AnalyzeConstructor(ctor, typeDecl, semanticModel, filePath);
            methodMetricsList.Add(metrics);
        }

        var typeName = typeSymbol?.ToDisplayString() ?? typeDecl.Identifier.Text;

        return new TypeMetrics(
            FullName: typeName,
            FilePath: filePath,
            DepthOfInheritance: dit,
            Methods: methodMetricsList);
    }

    private TypeMetrics? AnalyzeTopLevelStatements(SyntaxNode root, List<GlobalStatementSyntax> topLevelStatements, SemanticModel semanticModel, string filePath)
    {
        var methodMetricsList = new List<MethodMetrics>();

        // Analyze the top-level code as a single "Main" method
        var firstStatement = topLevelStatements.First();
        var lastStatement = topLevelStatements.Last();

        var cc = CyclomaticComplexityCalculator.Calculate(root);
        var loc = topLevelStatements.Sum(s => LinesOfCodeCalculator.Calculate(s));
        var halstead = HalsteadCalculator.Calculate(root);
        var mi = MaintainabilityIndexCalculator.Calculate(halstead.Volume, cc, loc);

        var startLine = firstStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine = lastStatement.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

        methodMetricsList.Add(new MethodMetrics(
            FullName: "<Program>$.Main(string[])",
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halstead.Volume,
            MaintainabilityIndex: mi));

        // Analyze local functions defined in top-level code
        var localFunctions = root.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Where(lf => !lf.Ancestors().OfType<TypeDeclarationSyntax>().Any());

        foreach (var localFunc in localFunctions)
        {
            var metrics = AnalyzeLocalFunction(localFunc, filePath);
            methodMetricsList.Add(metrics);
        }

        return new TypeMetrics(
            FullName: "<Program>$",
            FilePath: filePath,
            DepthOfInheritance: 0,
            Methods: methodMetricsList);
    }

    private MethodMetrics AnalyzeMethod(MethodDeclarationSyntax method, SemanticModel semanticModel, string filePath)
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

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halstead.Volume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeConstructor(ConstructorDeclarationSyntax ctor, TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
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

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halstead.Volume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeLocalFunction(LocalFunctionStatementSyntax localFunc, string filePath)
    {
        var fullName = $"<Program>$.{localFunc.Identifier.Text}()";

        var cc = localFunc.Body != null || localFunc.ExpressionBody != null
            ? CyclomaticComplexityCalculator.Calculate(localFunc)
            : 1;

        var loc = localFunc.Body != null
            ? LinesOfCodeCalculator.Calculate(localFunc.Body)
            : (localFunc.ExpressionBody != null ? 1 : 0);

        var halstead = HalsteadCalculator.Calculate(localFunc);
        var mi = MaintainabilityIndexCalculator.Calculate(halstead.Volume, cc, loc);

        var lineSpan = localFunc.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halstead.Volume,
            MaintainabilityIndex: mi);
    }
}
