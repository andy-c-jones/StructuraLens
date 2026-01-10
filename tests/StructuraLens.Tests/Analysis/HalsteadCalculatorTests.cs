using Microsoft.CodeAnalysis.CSharp;
using StructuraLens.Core.Analysis;

namespace StructuraLens.Tests.Analysis;

public class HalsteadCalculatorTests
{
    private static HalsteadCalculator.HalsteadMetrics CalculateFromCode(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return HalsteadCalculator.Calculate(root);
    }

    [Test]
    public async Task Calculate_EmptyMethod_ReturnsSmallVolume()
    {
        var code = @"
            public class Test
            {
                public void Empty() { }
            }";

        var metrics = CalculateFromCode(code);

        // Even an empty method has some tokens (braces, parens, keywords)
        // but the volume should be relatively small
        await Assert.That(metrics.Volume).IsGreaterThanOrEqualTo(0);
        await Assert.That(metrics.Volume).IsLessThan(50);
    }

    [Test]
    public async Task Calculate_SimpleAssignment_CountsOperatorsAndOperands()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    int x = 5;
                }
            }";

        var metrics = CalculateFromCode(code);

        // Operators: =, ;, {, }, (, )
        // Operands: Test, Method, int, x, 5
        await Assert.That(metrics.DistinctOperators).IsGreaterThan(0);
        await Assert.That(metrics.DistinctOperands).IsGreaterThan(0);
        await Assert.That(metrics.Volume).IsGreaterThan(0);
    }

    [Test]
    public async Task Calculate_ArithmeticExpression_CountsArithmeticOperators()
    {
        var code = @"
            public class Test
            {
                public int Method()
                {
                    int a = 1;
                    int b = 2;
                    return a + b * 2 - 1;
                }
            }";

        var metrics = CalculateFromCode(code);

        // Should count +, *, - as operators
        await Assert.That(metrics.TotalOperators).IsGreaterThan(5);
        await Assert.That(metrics.Volume).IsGreaterThan(0);
    }

    [Test]
    public async Task Calculate_ComparisonOperators_AreCountedAsOperators()
    {
        var code = @"
            public class Test
            {
                public bool Method()
                {
                    int x = 5;
                    return x > 3 && x < 10;
                }
            }";

        var metrics = CalculateFromCode(code);

        // Should count >, <, && as operators
        await Assert.That(metrics.DistinctOperators).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Calculate_Literals_AreCountedAsOperands()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    string s = ""hello"";
                    char c = 'x';
                    bool b = true;
                    object n = null;
                }
            }";

        var metrics = CalculateFromCode(code);

        // Should count "hello", 'x', true, null as operands
        await Assert.That(metrics.DistinctOperands).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task Calculate_ControlFlow_CountsKeywordsAsOperators()
    {
        var code = @"
            public class Test
            {
                public void Method(int x)
                {
                    if (x > 0)
                    {
                        while (x > 0)
                        {
                            x--;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 10; i++)
                        {
                        }
                    }
                }
            }";

        var metrics = CalculateFromCode(code);

        // Should count if, while, else, for as operators
        await Assert.That(metrics.TotalOperators).IsGreaterThan(10);
    }

    [Test]
    public async Task HalsteadMetrics_Vocabulary_SumsDistinctOperatorsAndOperands()
    {
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 10,
            TotalOperators: 20,
            TotalOperands: 30);

        await Assert.That(metrics.Vocabulary).IsEqualTo(15);
    }

    [Test]
    public async Task HalsteadMetrics_Length_SumsTotalOperatorsAndOperands()
    {
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 10,
            TotalOperators: 20,
            TotalOperands: 30);

        await Assert.That(metrics.Length).IsEqualTo(50);
    }

    [Test]
    public async Task HalsteadMetrics_Volume_CalculatesCorrectly()
    {
        // V = N * log2(n) where N=50, n=15
        // V = 50 * log2(15) = 50 * 3.906... ≈ 195.3
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 10,
            TotalOperators: 20,
            TotalOperands: 30);

        var expectedVolume = 50 * Math.Log2(15);
        await Assert.That(Math.Abs(metrics.Volume - expectedVolume)).IsLessThan(0.01);
    }

    [Test]
    public async Task HalsteadMetrics_Difficulty_CalculatesCorrectly()
    {
        // D = (n1/2) * (N2/n2) where n1=5, N2=30, n2=10
        // D = (5/2) * (30/10) = 2.5 * 3 = 7.5
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 10,
            TotalOperators: 20,
            TotalOperands: 30);

        await Assert.That(metrics.Difficulty).IsEqualTo(7.5);
    }

    [Test]
    public async Task HalsteadMetrics_Effort_CalculatesCorrectly()
    {
        // E = D * V
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 10,
            TotalOperators: 20,
            TotalOperands: 30);

        var expectedEffort = metrics.Difficulty * metrics.Volume;
        await Assert.That(metrics.Effort).IsEqualTo(expectedEffort);
    }

    [Test]
    public async Task HalsteadMetrics_ZeroOperands_ReturnsDifficultyZero()
    {
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 5,
            DistinctOperands: 0,
            TotalOperators: 10,
            TotalOperands: 0);

        await Assert.That(metrics.Difficulty).IsEqualTo(0);
    }

    [Test]
    public async Task HalsteadMetrics_LowVocabulary_ReturnsVolumeZero()
    {
        var metrics = new HalsteadCalculator.HalsteadMetrics(
            DistinctOperators: 1,
            DistinctOperands: 0,
            TotalOperators: 1,
            TotalOperands: 0);

        await Assert.That(metrics.Volume).IsEqualTo(0);
    }
}
