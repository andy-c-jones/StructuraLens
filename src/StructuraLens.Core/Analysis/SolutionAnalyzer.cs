using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger _logger;

    public SolutionAnalyzer(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

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
        _logger.LogInformation("Starting solution analysis: {SolutionPath}", solutionPath);
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Solution file not found: {fullPath}");
        }

        // Restore NuGet packages to ensure all references are available
        _logger.LogInformation("Restoring NuGet packages...");
        await RestorePackagesAsync(fullPath, cancellationToken);

        _logger.LogInformation("Loading solution into MSBuild workspace...");
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                _logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
            }
        });

        var solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        _logger.LogInformation("Loaded solution with {ProjectCount} C# projects", csharpProjects.Count);

        var projectMetricsList = new List<ProjectMetrics>();
        var projectIndex = 0;

        foreach (var project in csharpProjects)
        {
            projectIndex++;
            _logger.LogInformation("Analyzing project {Index}/{Total}: {ProjectName}", projectIndex, csharpProjects.Count, project.Name);
            
            var projectMetrics = await AnalyzeProjectAsync(project, cancellationToken);
            projectMetricsList.Add(projectMetrics);
            
            _logger.LogInformation("Completed {ProjectName}: {TypeCount} types, {MethodCount} methods", 
                project.Name, 
                projectMetrics.Types.Count, 
                projectMetrics.TotalMethods);
        }

        // Analyze coupling across the entire solution with configuration
        _logger.LogInformation("Analyzing solution-wide coupling with mode: {CouplingMode}", config.Coupling.Mode);
        var couplingAnalysis = await CouplingAnalyzer.AnalyzeSolutionAsync(solution, config, _logger, cancellationToken);

        // Run architecture linting if rules are configured
        LintingResults? lintingResults = null;
        if (config.Rules.Count > 0)
        {
            _logger.LogInformation("Evaluating {RuleCount} architecture rules...", config.Rules.Count);
            lintingResults = ArchitectureLinter.Evaluate(couplingAnalysis, config.Rules);
            _logger.LogInformation("Linting complete: {ErrorCount} errors, {WarningCount} warnings", 
                lintingResults.ErrorCount, 
                lintingResults.WarningCount);
        }

        _logger.LogInformation("Analysis complete. Total: {ProjectCount} projects, {TypeCount} types, {MethodCount} methods",
            projectMetricsList.Count,
            projectMetricsList.Sum(p => p.Types.Count),
            projectMetricsList.Sum(p => p.TotalMethods));

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: projectMetricsList,
            Warnings: _warnings)
        {
            CouplingAnalysis = couplingAnalysis,
            LintingResults = lintingResults
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
        _logger.LogInformation("Starting project analysis: {ProjectPath}", projectPath);
        EnsureMSBuildRegistered();

        var fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Project file not found: {fullPath}");
        }

        // Restore NuGet packages to ensure all references are available
        _logger.LogInformation("Restoring NuGet packages...");
        await RestorePackagesAsync(fullPath, cancellationToken);

        _logger.LogInformation("Loading project into MSBuild workspace...");
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                _warnings.Add($"Workspace warning: {e.Diagnostic.Message}");
                _logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
            }
        });

        var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
        _logger.LogInformation("Analyzing project: {ProjectName}", project.Name);
        
        var projectMetrics = await AnalyzeProjectAsync(project, cancellationToken);

        // Analyze internal coupling within the project with configuration
        _logger.LogInformation("Analyzing project coupling with mode: {CouplingMode}", config.Coupling.Mode);
        var couplingAnalysis = await CouplingAnalyzer.AnalyzeProjectCouplingAsync(project, config, _logger, cancellationToken);

        // Run architecture linting if rules are configured
        LintingResults? lintingResults = null;
        if (config.Rules.Count > 0)
        {
            _logger.LogInformation("Evaluating {RuleCount} architecture rules...", config.Rules.Count);
            lintingResults = ArchitectureLinter.Evaluate(couplingAnalysis, config.Rules);
            _logger.LogInformation("Linting complete: {ErrorCount} errors, {WarningCount} warnings", 
                lintingResults.ErrorCount, 
                lintingResults.WarningCount);
        }

        return new AnalysisReport(
            SolutionPath: fullPath,
            AnalyzedAt: DateTime.UtcNow,
            Projects: [projectMetrics],
            Warnings: _warnings)
        {
            CouplingAnalysis = couplingAnalysis,
            LintingResults = lintingResults
        };
    }

    private async Task<ProjectMetrics> AnalyzeProjectAsync(Project project, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting compilation for project: {ProjectName}", project.Name);
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            _warnings.Add($"Could not get compilation for project: {project.Name}");
            _logger.LogWarning("Could not get compilation for project: {ProjectName}", project.Name);
            return new ProjectMetrics(project.Name, project.FilePath ?? "", []);
        }

        // Collect diagnostics from compilation
        var diagnosticSummary = CollectDiagnostics(compilation);

        var typeMetricsList = new List<TypeMetrics>();
        var documentCount = project.Documents.Count();
        var documentIndex = 0;

        _logger.LogDebug("Analyzing {DocumentCount} documents in project {ProjectName}", documentCount, project.Name);

        foreach (var document in project.Documents)
        {
            documentIndex++;
            if (document.SourceCodeKind != SourceCodeKind.Regular)
                continue;

            if (documentIndex % 50 == 0)
            {
                _logger.LogDebug("Progress: {DocumentIndex}/{DocumentCount} documents processed in {ProjectName}", 
                    documentIndex, documentCount, project.Name);
            }

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
            Types: typeMetricsList)
        {
            Diagnostics = diagnosticSummary
        };
    }

    private static DiagnosticSummary CollectDiagnostics(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden || d.Id.StartsWith("CS"))
            .Select(d => new DiagnosticInfo(
                Id: d.Id,
                Message: d.GetMessage(),
                Severity: MapSeverity(d.Severity),
                FilePath: d.Location.SourceTree?.FilePath ?? "",
                Line: d.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column: d.Location.GetLineSpan().StartLinePosition.Character + 1)
            {
                Category = d.Descriptor.Category,
                HelpLink = d.Descriptor.HelpLinkUri
            })
            .ToList();

        return new DiagnosticSummary
        {
            ErrorCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Error),
            WarningCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Warning),
            InfoCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Info),
            HiddenCount = diagnostics.Count(d => d.Severity == DiagnosticLevel.Hidden),
            Diagnostics = diagnostics
        };
    }

    private static DiagnosticLevel MapSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => DiagnosticLevel.Error,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticLevel.Warning,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => DiagnosticLevel.Info,
            _ => DiagnosticLevel.Hidden
        };
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

    private async Task RestorePackagesAsync(string projectOrSolutionPath, CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectOrSolutionPath}\" --verbosity quiet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogWarning("Failed to start dotnet restore process");
            return;
        }

        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogWarning("Package restore completed with exit code {ExitCode}: {Error}", process.ExitCode, error);
        }
        else
        {
            _logger.LogDebug("Package restore completed successfully");
        }
    }
}
