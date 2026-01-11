using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Unified metrics calculator that computes Cyclomatic Complexity, Lines of Code,
/// and Halstead metrics in a single syntax tree traversal for improved performance.
/// </summary>
public static class UnifiedMetricsCalculator
{
    /// <summary>
    /// Calculates all code metrics for a syntax node in a single pass.
    /// </summary>
    public static UnifiedMetrics Calculate(SyntaxNode node)
    {
        var walker = new UnifiedMetricsWalker();
        walker.Visit(node);
        return walker.GetMetrics();
    }

    /// <summary>
    /// Container for all computed metrics.
    /// </summary>
    public readonly record struct UnifiedMetrics(
        int CyclomaticComplexity,
        int LinesOfCode,
        int DistinctOperators,
        int DistinctOperands,
        int TotalOperators,
        int TotalOperands)
    {
        public int Vocabulary => DistinctOperators + DistinctOperands;
        public int Length => TotalOperators + TotalOperands;
        
        public double HalsteadVolume => Length > 0 && Vocabulary > 1 
            ? Length * Math.Log2(Vocabulary) 
            : 0;
        
        public double HalsteadDifficulty => DistinctOperands > 0 
            ? (DistinctOperators / 2.0) * ((double)TotalOperands / DistinctOperands) 
            : 0;
        
        public double HalsteadEffort => HalsteadDifficulty * HalsteadVolume;

        public double MaintainabilityIndex => CalculateMaintainabilityIndex();

        private double CalculateMaintainabilityIndex()
        {
            if (HalsteadVolume <= 0 || LinesOfCode <= 0)
                return 100.0;

            var lnVolume = Math.Log(HalsteadVolume);
            var lnLoc = Math.Log(LinesOfCode);
            var rawMi = 171 - (5.2 * lnVolume) - (0.23 * CyclomaticComplexity) - (16.2 * lnLoc);
            var normalizedMi = 100.0 * rawMi / 171.0;
            return Math.Max(0, Math.Min(100, normalizedMi));
        }
    }

    /// <summary>
    /// Unified syntax walker that computes all metrics in a single pass.
    /// Uses SyntaxWalkerDepth.Token to also capture Halstead operands/operators.
    /// </summary>
    private sealed class UnifiedMetricsWalker : CSharpSyntaxWalker
    {
        private int _cyclomaticComplexity = 1;
        private int _linesOfCode;
        private readonly HashSet<string> _distinctOperators = [];
        private readonly HashSet<string> _distinctOperands = [];
        private int _totalOperators;
        private int _totalOperands;

        public UnifiedMetricsWalker() : base(SyntaxWalkerDepth.Token) { }

        public UnifiedMetrics GetMetrics() => new(
            CyclomaticComplexity: _cyclomaticComplexity,
            LinesOfCode: _linesOfCode,
            DistinctOperators: _distinctOperators.Count,
            DistinctOperands: _distinctOperands.Count,
            TotalOperators: _totalOperators,
            TotalOperands: _totalOperands);

        // === Cyclomatic Complexity ===

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            _cyclomaticComplexity++;
            _linesOfCode++;
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            _cyclomaticComplexity++;
            _linesOfCode++;
            base.VisitWhileStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            _cyclomaticComplexity++;
            _linesOfCode++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            _cyclomaticComplexity++;
            _linesOfCode++;
            base.VisitForEachStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            _cyclomaticComplexity++;
            _linesOfCode++;
            base.VisitDoStatement(node);
        }

        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitSwitchSection(node);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitCatchClause(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitConditionalExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression) ||
                node.IsKind(SyntaxKind.CoalesceExpression))
            {
                _cyclomaticComplexity++;
            }
            base.VisitBinaryExpression(node);
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            _cyclomaticComplexity++;
            base.VisitConditionalAccessExpression(node);
        }

        // === Lines of Code (statements that don't also increment CC) ===

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitExpressionStatement(node);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitLocalDeclarationStatement(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitReturnStatement(node);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitSwitchStatement(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitThrowStatement(node);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitTryStatement(node);
        }

        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitUsingStatement(node);
        }

        public override void VisitLockStatement(LockStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitLockStatement(node);
        }

        public override void VisitBreakStatement(BreakStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitBreakStatement(node);
        }

        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitContinueStatement(node);
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            _linesOfCode++;
            base.VisitYieldStatement(node);
        }

        // === Halstead Metrics (token-level) ===

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
