using Microsoft.CodeAnalysis.CSharp;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class CyclomaticComplexityCalculatorTests
{
    [Test]
    public async Task Calculate_EmptyMethod_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public void EmptyMethod() { }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_SingleIfStatement_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithIf(bool condition)
                {
                    if (condition) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_IfElseStatement_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithIfElse(bool condition)
                {
                    if (condition) { }
                    else { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_NestedIfStatements_ReturnsThree()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithNestedIf(bool a, bool b)
                {
                    if (a)
                    {
                        if (b) { }
                    }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(3);
    }

    [Test]
    public async Task Calculate_WhileLoop_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithWhile(bool condition)
                {
                    while (condition) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ForLoop_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithFor()
                {
                    for (int i = 0; i < 10; i++) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ForEachLoop_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithForEach(int[] items)
                {
                    foreach (var item in items) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_DoWhileLoop_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithDoWhile(bool condition)
                {
                    do { } while (condition);
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_SwitchWithThreeCases_ReturnsFour()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithSwitch(int value)
                {
                    switch (value)
                    {
                        case 1: break;
                        case 2: break;
                        default: break;
                    }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(4);
    }

    [Test]
    public async Task Calculate_TryCatch_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithTryCatch()
                {
                    try { }
                    catch (System.Exception) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_TryMultipleCatches_ReturnsThree()
    {
        var code = """
            public class TestClass
            {
                public void MethodWithMultipleCatches()
                {
                    try { }
                    catch (System.ArgumentException) { }
                    catch (System.Exception) { }
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(3);
    }

    [Test]
    public async Task Calculate_LogicalAndOperator_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public bool MethodWithLogicalAnd(bool a, bool b)
                {
                    return a && b;
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_LogicalOrOperator_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public bool MethodWithLogicalOr(bool a, bool b)
                {
                    return a || b;
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_TernaryOperator_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public int MethodWithTernary(bool condition)
                {
                    return condition ? 1 : 0;
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_NullCoalescingOperator_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public string MethodWithNullCoalescing(string? value)
                {
                    return value ?? "default";
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ConditionalAccess_ReturnsTwo()
    {
        var code = """
            public class TestClass
            {
                public int? MethodWithConditionalAccess(string? value)
                {
                    return value?.Length;
                }
            }
            """;

        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ComplexMethod_ReturnsCorrectValue()
    {
        var code = """
            public class TestClass
            {
                public int ComplexMethod(int x, bool flag)
                {
                    if (x > 0 && flag)
                    {
                        for (int i = 0; i < x; i++)
                        {
                            if (i % 2 == 0)
                            {
                                return i;
                            }
                        }
                    }
                    else if (x < 0)
                    {
                        return -1;
                    }
                    return 0;
                }
            }
            """;

        // 1 base + if + && + for + if + else if = 6
        var cc = CalculateForMethod(code);
        await Assert.That(cc).IsEqualTo(6);
    }

    private static int CalculateForMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        return CyclomaticComplexityCalculator.Calculate(method);
    }
}
