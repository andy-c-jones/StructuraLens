using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Computes Halstead complexity metrics for a method.
/// 
/// Halstead metrics:
/// - n1 = number of distinct operators
/// - n2 = number of distinct operands
/// - N1 = total number of operators
/// - N2 = total number of operands
/// - Program vocabulary: n = n1 + n2
/// - Program length: N = N1 + N2
/// - Volume: V = N * log2(n)
/// - Difficulty: D = (n1 / 2) * (N2 / n2)
/// - Effort: E = D * V
/// </summary>
public static class HalsteadCalculator
{
    public static HalsteadMetrics Calculate(SyntaxNode node)
    {
        var walker = new HalsteadWalker();
        walker.Visit(node);
        return walker.GetMetrics();
    }

    public record HalsteadMetrics(
        int DistinctOperators,
        int DistinctOperands,
        int TotalOperators,
        int TotalOperands)
    {
        public int Vocabulary => DistinctOperators + DistinctOperands;
        public int Length => TotalOperators + TotalOperands;
        
        public double Volume => Length > 0 && Vocabulary > 1 
            ? Length * Math.Log2(Vocabulary) 
            : 0;
        
        public double Difficulty => DistinctOperands > 0 
            ? (DistinctOperators / 2.0) * ((double)TotalOperands / DistinctOperands) 
            : 0;
        
        public double Effort => Difficulty * Volume;
    }

    private sealed class HalsteadWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _distinctOperators = [];
        private readonly HashSet<string> _distinctOperands = [];
        private int _totalOperators;
        private int _totalOperands;

        public HalsteadWalker() : base(SyntaxWalkerDepth.Token) { }

        public HalsteadMetrics GetMetrics() => new(
            DistinctOperators: _distinctOperators.Count,
            DistinctOperands: _distinctOperands.Count,
            TotalOperators: _totalOperators,
            TotalOperands: _totalOperands);

        public override void VisitToken(SyntaxToken token)
        {
            if (IsOperator(token))
            {
                _distinctOperators.Add(token.Text);
                _totalOperators++;
            }
            else if (IsOperand(token))
            {
                _distinctOperands.Add(token.Text);
                _totalOperands++;
            }

            base.VisitToken(token);
        }

        private static bool IsOperator(SyntaxToken token)
        {
            return token.Kind() switch
            {
                // Arithmetic operators
                SyntaxKind.PlusToken or
                SyntaxKind.MinusToken or
                SyntaxKind.AsteriskToken or
                SyntaxKind.SlashToken or
                SyntaxKind.PercentToken or
                SyntaxKind.PlusPlusToken or
                SyntaxKind.MinusMinusToken or

                // Assignment operators
                SyntaxKind.EqualsToken or
                SyntaxKind.PlusEqualsToken or
                SyntaxKind.MinusEqualsToken or
                SyntaxKind.AsteriskEqualsToken or
                SyntaxKind.SlashEqualsToken or
                SyntaxKind.PercentEqualsToken or
                SyntaxKind.AmpersandEqualsToken or
                SyntaxKind.BarEqualsToken or
                SyntaxKind.CaretEqualsToken or
                SyntaxKind.LessThanLessThanEqualsToken or
                SyntaxKind.GreaterThanGreaterThanEqualsToken or
                SyntaxKind.QuestionQuestionEqualsToken or

                // Comparison operators
                SyntaxKind.EqualsEqualsToken or
                SyntaxKind.ExclamationEqualsToken or
                SyntaxKind.LessThanToken or
                SyntaxKind.GreaterThanToken or
                SyntaxKind.LessThanEqualsToken or
                SyntaxKind.GreaterThanEqualsToken or

                // Logical operators
                SyntaxKind.AmpersandAmpersandToken or
                SyntaxKind.BarBarToken or
                SyntaxKind.ExclamationToken or

                // Bitwise operators
                SyntaxKind.AmpersandToken or
                SyntaxKind.BarToken or
                SyntaxKind.CaretToken or
                SyntaxKind.TildeToken or
                SyntaxKind.LessThanLessThanToken or
                SyntaxKind.GreaterThanGreaterThanToken or

                // Other operators
                SyntaxKind.QuestionToken or
                SyntaxKind.ColonToken or
                SyntaxKind.QuestionQuestionToken or
                SyntaxKind.DotToken or
                SyntaxKind.MinusGreaterThanToken or
                SyntaxKind.EqualsGreaterThanToken or

                // Keywords that act as operators
                SyntaxKind.NewKeyword or
                SyntaxKind.TypeOfKeyword or
                SyntaxKind.SizeOfKeyword or
                SyntaxKind.NameOfKeyword or
                SyntaxKind.IsKeyword or
                SyntaxKind.AsKeyword or
                SyntaxKind.AwaitKeyword or
                SyntaxKind.ThrowKeyword or
                SyntaxKind.ReturnKeyword or
                SyntaxKind.BreakKeyword or
                SyntaxKind.ContinueKeyword or
                SyntaxKind.GotoKeyword or

                // Control flow keywords (also operators in Halstead)
                SyntaxKind.IfKeyword or
                SyntaxKind.ElseKeyword or
                SyntaxKind.SwitchKeyword or
                SyntaxKind.CaseKeyword or
                SyntaxKind.DefaultKeyword or
                SyntaxKind.WhileKeyword or
                SyntaxKind.DoKeyword or
                SyntaxKind.ForKeyword or
                SyntaxKind.ForEachKeyword or
                SyntaxKind.TryKeyword or
                SyntaxKind.CatchKeyword or
                SyntaxKind.FinallyKeyword or
                SyntaxKind.UsingKeyword or
                SyntaxKind.LockKeyword or

                // Brackets and delimiters as operators
                SyntaxKind.OpenParenToken or
                SyntaxKind.CloseParenToken or
                SyntaxKind.OpenBracketToken or
                SyntaxKind.CloseBracketToken or
                SyntaxKind.CommaToken or
                SyntaxKind.SemicolonToken => true,

                _ => false
            };
        }

        private static bool IsOperand(SyntaxToken token)
        {
            return token.Kind() switch
            {
                // Identifiers
                SyntaxKind.IdentifierToken or

                // Literals
                SyntaxKind.NumericLiteralToken or
                SyntaxKind.StringLiteralToken or
                SyntaxKind.CharacterLiteralToken or
                SyntaxKind.TrueKeyword or
                SyntaxKind.FalseKeyword or
                SyntaxKind.NullKeyword or
                SyntaxKind.DefaultKeyword or
                SyntaxKind.ThisKeyword or
                SyntaxKind.BaseKeyword => true,

                _ => false
            };
        }
    }
}
