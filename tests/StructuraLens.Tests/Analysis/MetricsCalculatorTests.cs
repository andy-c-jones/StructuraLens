using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class MetricsCalculatorTests
{
    private readonly MetricsCalculator _calculator = new();

    [Test]
    public async Task CalculateCyclomaticComplexity_WithSimpleMethod_ReturnsCorrectValue()
    {
        // Arrange
        var code = """
            public class TestClass
            {
                public void SimpleMethod(bool condition)
                {
                    if (condition) { }
                }
            }
            """;
        var method = ParseMethod(code);

        // Act
        var cc = _calculator.CalculateCyclomaticComplexity(method);

        // Assert - Base (1) + if (1) = 2
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task CalculateLinesOfCode_WithSampleCode_ReturnsExpectedValue()
    {
        // Arrange
        var code = """
            public class TestClass
            {
                public void Method()
                {
                    int x = 1;
                    int y = 2;
                    int z = x + y;
                }
            }
            """;
        var method = ParseMethod(code);

        // Act
        var loc = _calculator.CalculateLinesOfCode(method);

        // Assert - Should count executable statements
        await Assert.That(loc).IsGreaterThan(0);
    }

    [Test]
    public async Task CalculateHalstead_WithSampleCode_ReturnsExpectedMetrics()
    {
        // Arrange
        var code = """
            public class TestClass
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """;
        var method = ParseMethod(code);

        // Act
        var halstead = _calculator.CalculateHalstead(method);

        // Assert - Verify tuple structure and that values are reasonable
        await Assert.That(halstead.Operators).IsGreaterThan(0);
        await Assert.That(halstead.Operands).IsGreaterThan(0);
        await Assert.That(halstead.Volume).IsGreaterThan(0);
        await Assert.That(halstead.Difficulty).IsGreaterThan(0);
        await Assert.That(halstead.Effort).IsGreaterThan(0);
    }

    [Test]
    public async Task CalculateMaintainabilityIndex_WithValidInputs_ReturnsExpectedValue()
    {
        // Arrange
        double halsteadVolume = 100.0;
        int cyclomaticComplexity = 5;
        int linesOfCode = 50;

        // Act
        var mi = _calculator.CalculateMaintainabilityIndex(halsteadVolume, cyclomaticComplexity, linesOfCode);

        // Assert - MI should be between 0 and 100
        await Assert.That(mi).IsGreaterThanOrEqualTo(0);
        await Assert.That(mi).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task CalculateDepthOfInheritance_WithTypeSymbol_ReturnsExpectedDepth()
    {
        // Arrange
        var code = """
            public class Base { }
            public class Derived : Base { }
            public class DoubleDerived : Derived { }
            """;
        
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);
        
        var semanticModel = compilation.GetSemanticModel(tree);
        var doubleDerivedClass = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Last();
        
        var typeSymbol = semanticModel.GetDeclaredSymbol(doubleDerivedClass) as INamedTypeSymbol;

        // Act
        var dit = _calculator.CalculateDepthOfInheritance(typeSymbol);

        // Assert - DoubleDerived -> Derived -> Base (not counting Object) = depth 2
        await Assert.That(dit).IsEqualTo(2);
    }

    [Test]
    public async Task CalculateUnifiedMetrics_WithSampleCode_ReturnsAllMetrics()
    {
        // Arrange
        var code = """
            public class TestClass
            {
                public void Method(bool condition)
                {
                    if (condition)
                    {
                        int x = 1;
                        int y = 2;
                    }
                }
            }
            """;
        var method = ParseMethod(code);

        // Act
        var metrics = _calculator.CalculateUnifiedMetrics(method);

        // Assert - Verify all components of unified metrics
        await Assert.That(metrics.CyclomaticComplexity).IsGreaterThan(0);
        await Assert.That(metrics.LinesOfCode).IsGreaterThan(0);
        await Assert.That(metrics.HalsteadVolume).IsGreaterThan(0);
        await Assert.That(metrics.MaintainabilityIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(metrics.MaintainabilityIndex).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task CalculateUnifiedMetrics_VsIndividualCalculations_ProducesSameResults()
    {
        // Arrange
        var code = """
            public class TestClass
            {
                public int ComplexMethod(int x, int y)
                {
                    if (x > 0)
                    {
                        return x + y;
                    }
                    else if (x < 0)
                    {
                        return x - y;
                    }
                    return 0;
                }
            }
            """;
        var method = ParseMethod(code);

        // Act
        var unified = _calculator.CalculateUnifiedMetrics(method);
        var cc = _calculator.CalculateCyclomaticComplexity(method);
        var loc = _calculator.CalculateLinesOfCode(method);
        var halstead = _calculator.CalculateHalstead(method);
        var mi = _calculator.CalculateMaintainabilityIndex(halstead.Volume, cc, loc);

        // Assert - Unified metrics should match individual calculations
        await Assert.That(unified.CyclomaticComplexity).IsEqualTo(cc);
        await Assert.That(unified.LinesOfCode).IsEqualTo(loc);
        await Assert.That(unified.HalsteadVolume).IsEqualTo(halstead.Volume);
        await Assert.That(unified.MaintainabilityIndex).IsEqualTo(mi);
    }

    private static MethodDeclarationSyntax ParseMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();
    }
}
