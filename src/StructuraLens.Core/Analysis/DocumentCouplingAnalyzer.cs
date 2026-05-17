using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Syntax walker that analyzes coupling within a single document.
/// </summary>
internal sealed class DocumentCouplingAnalyzer : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _filePath;
    private readonly List<DependencyEdge>? _dependencies;
    private readonly IDependencyCollector? _collector;
    private readonly string? _primaryNamespace;

    // Cache for ToDisplayString() results to avoid repeated expensive calls.
    private readonly Dictionary<ISymbol, string> _symbolDisplayCache = new(SymbolEqualityComparer.Default);
    // Cache containing type per TypeDeclarationSyntax to avoid repeated lookups.
    private readonly Dictionary<TypeDeclarationSyntax, string?> _containingTypeCache = [];

    /// <summary>
    /// Gets collected dependencies. Only valid when using list-based collection (not streaming).
    /// </summary>
    public IReadOnlyList<DependencyEdge> Dependencies => _dependencies ?? [];

    /// <summary>
    /// Constructor for list-based collection (backward compatibility).
    /// </summary>
    public DocumentCouplingAnalyzer(SemanticModel semanticModel, string filePath, SyntaxNode root)
        : this(semanticModel, filePath, root, null)
    {
    }

    /// <summary>
    /// Constructor for streaming collection (memory-efficient).
    /// </summary>
    public DocumentCouplingAnalyzer(
        SemanticModel semanticModel,
        string filePath,
        SyntaxNode root,
        IDependencyCollector? collector)
    {
        _semanticModel = semanticModel;
        _filePath = filePath;
        _collector = collector;

        // Only create list if not using collector.
        if (collector == null)
        {
            _dependencies = [];
        }

        // Pre-scan for file-level namespace (file-scoped or first traditional namespace).
        _primaryNamespace = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString()
            ?? root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            var namespaceName = node.Name.ToString();
            var containingNamespace = GetContainingNamespace(node);

            if (!string.IsNullOrEmpty(containingNamespace) && containingNamespace != namespaceName)
            {
                AddDependencyEdge(new DependencyEdge(
                    FromEntity: containingNamespace,
                    ToEntity: namespaceName,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = DependencyEdge.EnableDetails
                        ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                        : null
                });
            }
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitQualifiedName(QualifiedNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitQualifiedName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
        {
            AnalyzeTypeReference(node, typeSymbol);
        }

        base.VisitGenericName(node);
    }

    /// <summary>
    /// Adds a dependency edge, routing to collector or list based on mode.
    /// </summary>
    private void AddDependencyEdge(DependencyEdge edge)
    {
        if (_collector != null)
        {
            _collector.AddDependency(edge);
        }
        else
        {
            _dependencies?.Add(edge);
        }
    }

    /// <summary>
    /// Gets cached display string for a symbol, or computes and caches it.
    /// </summary>
    private string GetDisplayString(ISymbol symbol)
    {
        if (_symbolDisplayCache.TryGetValue(symbol, out var cached))
        {
            return cached;
        }

        var displayString = symbol.ToDisplayString();
        _symbolDisplayCache[symbol] = displayString;
        return displayString;
    }

    private void AnalyzeTypeReference(SyntaxNode node, ITypeSymbol typeSymbol)
    {
        var fromType = GetContainingType(node);
        var toType = GetDisplayString(typeSymbol);

        if (fromType != null && fromType != toType)
        {
            AddDependencyEdge(new DependencyEdge(
                FromEntity: fromType,
                ToEntity: toType,
                Type: DependencyType.TypeReference,
                ReferenceCount: 1)
            {
                SourceLocation = DependencyEdge.EnableDetails
                    ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                    : null,
                ReferencedSymbol = DependencyEdge.EnableDetails ? typeSymbol.Name : null
            });

            var fromNamespace = GetNamespace(fromType);
            var toNamespace = typeSymbol.ContainingNamespace != null
                ? GetDisplayString(typeSymbol.ContainingNamespace)
                : "";

            if (fromNamespace != toNamespace && !string.IsNullOrEmpty(toNamespace))
            {
                AddDependencyEdge(new DependencyEdge(
                    FromEntity: fromNamespace,
                    ToEntity: toNamespace,
                    Type: DependencyType.NamespaceReference,
                    ReferenceCount: 1)
                {
                    SourceLocation = DependencyEdge.EnableDetails
                        ? $"{_filePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"
                        : null,
                    ReferencedSymbol = DependencyEdge.EnableDetails ? typeSymbol.Name : null
                });
            }
        }
    }

    private string GetContainingNamespace(SyntaxNode node)
    {
        var namespaceDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        return _primaryNamespace ?? "";
    }

    private string? GetContainingType(SyntaxNode node)
    {
        var typeDecl = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
        {
            return null;
        }

        if (_containingTypeCache.TryGetValue(typeDecl, out var cached))
        {
            return cached;
        }

        var typeSymbol = _semanticModel.GetDeclaredSymbol(typeDecl) as ITypeSymbol;
        var result = typeSymbol != null ? GetDisplayString(typeSymbol) : null;
        _containingTypeCache[typeDecl] = result;
        return result;
    }

    private static string GetNamespace(string fullTypeName)
    {
        var lastDotIndex = fullTypeName.LastIndexOf('.');
        return lastDotIndex > 0 ? fullTypeName[..lastDotIndex] : "";
    }
}
