using Microsoft.CodeAnalysis;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Computes Depth of Inheritance (DIT) for a type.
/// DIT = number of ancestor classes from System.Object to this type.
/// </summary>
public static class DepthOfInheritanceCalculator
{
    public static int Calculate(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
            return 0;

        int depth = 0;
        var current = typeSymbol.BaseType;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }
}
