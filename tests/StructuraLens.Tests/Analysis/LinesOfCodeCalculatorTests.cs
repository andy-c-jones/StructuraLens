using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class LinesOfCodeCalculatorTests
{
    [Test]
    public async Task Calculate_EmptyMethod_ReturnsZero()
    {
        var code = """
            public class TestClass
            {
                public void EmptyMethod() { }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(0);
    }

    [Test]
    public async Task Calculate_SingleReturnStatement_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public int SingleReturn()
                {
                    return 42;
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_LocalDeclaration_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public void LocalDecl()
                {
                    int x = 5;
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_ExpressionStatement_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public void ExpressionStmt()
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_IfStatement_CountsIfAndBody()
    {
        var code = """
            public class TestClass
            {
                public void IfMethod(bool condition)
                {
                    if (condition)
                    {
                        Console.WriteLine("True");
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // if statement + expression statement inside
        await Assert.That(loc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ForLoop_CountsForAndBody()
    {
        var code = """
            public class TestClass
            {
                public void ForMethod()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // for statement + expression statement inside
        await Assert.That(loc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_WhileLoop_CountsWhileAndBody()
    {
        var code = """
            public class TestClass
            {
                public void WhileMethod(bool condition)
                {
                    while (condition)
                    {
                        break;
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // while statement + break statement
        await Assert.That(loc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_TryStatement_CountsTryAndCatchBodies()
    {
        var code = """
            public class TestClass
            {
                public void TryMethod()
                {
                    try
                    {
                        Console.WriteLine("Try");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Catch");
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // try statement + 2 expression statements
        await Assert.That(loc).IsEqualTo(3);
    }

    [Test]
    public async Task Calculate_SwitchStatement_CountsSwitchAndCaseBodies()
    {
        var code = """
            public class TestClass
            {
                public void SwitchMethod(int value)
                {
                    switch (value)
                    {
                        case 1:
                            Console.WriteLine("One");
                            break;
                        default:
                            break;
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // switch + expression + break + break
        await Assert.That(loc).IsEqualTo(4);
    }

    [Test]
    public async Task Calculate_UsingStatement_CountsUsingAndBody()
    {
        var code = """
            public class TestClass
            {
                public void UsingMethod()
                {
                    using (var stream = new System.IO.MemoryStream())
                    {
                        stream.Flush();
                    }
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // using statement + expression statement
        await Assert.That(loc).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_ThrowStatement_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public void ThrowMethod()
                {
                    throw new System.Exception();
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_YieldReturn_ReturnsOne()
    {
        var code = """
            public class TestClass
            {
                public System.Collections.Generic.IEnumerable<int> YieldMethod()
                {
                    yield return 1;
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        await Assert.That(loc).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_ComplexMethod_ReturnsCorrectCount()
    {
        var code = """
            public class TestClass
            {
                public int ComplexMethod(int x)
                {
                    int result = 0;
                    if (x > 0)
                    {
                        for (int i = 0; i < x; i++)
                        {
                            result += i;
                        }
                    }
                    return result;
                }
            }
            """;

        var loc = CalculateForMethodBody(code);
        // local decl + if + for + expression statement + return = 5
        await Assert.That(loc).IsEqualTo(5);
    }

    private static int CalculateForMethodBody(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();

        if (method.Body == null)
            return 0;

        return LinesOfCodeCalculator.Calculate(method.Body);
    }
}
