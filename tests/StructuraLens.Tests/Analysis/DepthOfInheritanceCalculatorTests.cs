using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class DepthOfInheritanceCalculatorTests
{
    [Test]
    public async Task Calculate_NullSymbol_ReturnsZero()
    {
        var result = DepthOfInheritanceCalculator.Calculate(null);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Calculate_ClassWithNoBaseClass_ReturnsZero()
    {
        var code = """
            public class SimpleClass { }
            """;

        var dit = CalculateDIT(code, "SimpleClass");
        await Assert.That(dit).IsEqualTo(0);
    }

    [Test]
    public async Task Calculate_ClassInheritingFromOneLevel_ReturnsOne()
    {
        var code = """
            public class BaseClass { }
            public class DerivedClass : BaseClass { }
            """;

        var dit = CalculateDIT(code, "DerivedClass");
        await Assert.That(dit).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_ClassInheritingFromTwoLevels_ReturnsTwo()
    {
        var code = """
            public class GrandparentClass { }
            public class ParentClass : GrandparentClass { }
            public class ChildClass : ParentClass { }
            """;

        var dit = CalculateDIT(code, "ChildClass");
        await Assert.That(dit).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ClassInheritingFromThreeLevels_ReturnsThree()
    {
        var code = """
            public class Level0 { }
            public class Level1 : Level0 { }
            public class Level2 : Level1 { }
            public class Level3 : Level2 { }
            """;

        var dit = CalculateDIT(code, "Level3");
        await Assert.That(dit).IsEqualTo(3);
    }

    [Test]
    public async Task Calculate_StructWithNoBase_ReturnsOne()
    {
        // Structs implicitly inherit from System.ValueType
        var code = """
            public struct SimpleStruct { }
            """;

        var dit = CalculateDIT(code, "SimpleStruct");
        await Assert.That(dit).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_IntermediateClass_ReturnsCorrectDepth()
    {
        var code = """
            public class GrandparentClass { }
            public class ParentClass : GrandparentClass { }
            public class ChildClass : ParentClass { }
            """;

        var ditParent = CalculateDIT(code, "ParentClass");
        await Assert.That(ditParent).IsEqualTo(1);
    }

    private static int CalculateDIT(string code, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var typeDecl = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == typeName);

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        return DepthOfInheritanceCalculator.Calculate(typeSymbol);
    }
}
