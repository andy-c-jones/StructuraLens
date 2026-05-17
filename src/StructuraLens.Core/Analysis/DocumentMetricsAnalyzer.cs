using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

internal sealed class DocumentMetricsAnalyzer
{
    private readonly IMetricsCalculator _metricsCalculator;

    public DocumentMetricsAnalyzer(IMetricsCalculator metricsCalculator)
    {
        _metricsCalculator = metricsCalculator;
    }

    public List<TypeMetrics> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
    {
        var typeMetrics = new List<TypeMetrics>();

        // Single pass to collect all nodes of interest, avoiding redundant tree traversals.
        var descendantNodes = root.DescendantNodes().ToList();

        foreach (var typeDeclaration in descendantNodes.OfType<TypeDeclarationSyntax>())
        {
            typeMetrics.Add(AnalyzeTypeDeclaration(typeDeclaration, semanticModel, filePath));
        }

        var topLevelStatements = descendantNodes.OfType<GlobalStatementSyntax>().ToList();
        if (topLevelStatements.Count > 0)
        {
            var topLevelMetrics = AnalyzeTopLevelStatements(root, topLevelStatements, semanticModel, filePath);
            if (topLevelMetrics != null)
            {
                typeMetrics.Add(topLevelMetrics);
            }
        }

        return typeMetrics;
    }

    private TypeMetrics AnalyzeTypeDeclaration(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        var dit = _metricsCalculator.CalculateDepthOfInheritance(typeSymbol);

        var methodMetricsList = new List<MethodMetrics>();

        // Single pass to collect all type members, avoiding redundant tree traversals.
        var members = typeDecl.DescendantNodes().ToList();

        foreach (var method in members.OfType<MethodDeclarationSyntax>())
        {
            methodMetricsList.Add(AnalyzeMethod(method, semanticModel, filePath));
        }

        foreach (var ctor in members.OfType<ConstructorDeclarationSyntax>())
        {
            methodMetricsList.Add(AnalyzeConstructor(ctor, typeDecl, semanticModel, filePath));
        }

        var typeName = typeSymbol?.ToDisplayString() ?? typeDecl.Identifier.Text;

        return new TypeMetrics(
            FullName: typeName,
            FilePath: filePath,
            DepthOfInheritance: dit,
            Methods: methodMetricsList);
    }

    private TypeMetrics? AnalyzeTopLevelStatements(
        SyntaxNode root,
        List<GlobalStatementSyntax> topLevelStatements,
        SemanticModel semanticModel,
        string filePath)
    {
        var methodMetricsList = new List<MethodMetrics>();

        var firstStatement = topLevelStatements.First();
        var lastStatement = topLevelStatements.Last();

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            root,
            hasBody: topLevelStatements.Count > 0,
            expressionBodiedFallbackLoc: 1);

        var startLine = firstStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine = lastStatement.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

        methodMetricsList.Add(new MethodMetrics(
            FullName: "<Program>$.Main(string[])",
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi));

        var localFunctions = root.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Where(lf => !lf.Ancestors().OfType<TypeDeclarationSyntax>().Any());

        foreach (var localFunc in localFunctions)
        {
            methodMetricsList.Add(AnalyzeLocalFunction(localFunc, filePath));
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

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            method,
            hasBody: method.Body != null || method.ExpressionBody != null,
            expressionBodiedFallbackLoc: method.Body != null ? 0 : 1);

        var lineSpan = method.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeConstructor(ConstructorDeclarationSyntax ctor, TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, string filePath)
    {
        var ctorSymbol = semanticModel.GetDeclaredSymbol(ctor);
        var fullName = ctorSymbol?.ToDisplayString() ?? $"{typeDecl.Identifier.Text}.ctor";

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            ctor,
            hasBody: ctor.Body != null || ctor.ExpressionBody != null,
            expressionBodiedFallbackLoc: ctor.Body != null ? 0 : 1);

        var lineSpan = ctor.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private MethodMetrics AnalyzeLocalFunction(LocalFunctionStatementSyntax localFunc, string filePath)
    {
        var fullName = $"<Program>$.{localFunc.Identifier.Text}()";

        var (cc, loc, halsteadVolume, mi) = CalculateCodeMetrics(
            localFunc,
            hasBody: localFunc.Body != null || localFunc.ExpressionBody != null,
            expressionBodiedFallbackLoc: localFunc.Body != null ? 0 : 1);

        var lineSpan = localFunc.GetLocation().GetLineSpan();

        return new MethodMetrics(
            FullName: fullName,
            FilePath: filePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            CyclomaticComplexity: cc,
            LinesOfExecutableCode: loc,
            HalsteadVolume: halsteadVolume,
            MaintainabilityIndex: mi);
    }

    private (int CyclomaticComplexity, int LinesOfCode, double HalsteadVolume, double MaintainabilityIndex) CalculateCodeMetrics(
        SyntaxNode node,
        bool hasBody,
        int expressionBodiedFallbackLoc)
    {
        if (!hasBody)
        {
            return (1, 0, 0, 100.0);
        }

        var metrics = _metricsCalculator.CalculateUnifiedMetrics(node);
        var loc = metrics.LinesOfCode > 0 ? metrics.LinesOfCode : expressionBodiedFallbackLoc;
        return (
            metrics.CyclomaticComplexity,
            loc,
            metrics.HalsteadVolume,
            metrics.MaintainabilityIndex);
    }
}
