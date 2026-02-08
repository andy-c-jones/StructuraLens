using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq; // TEST: Unnecessary using - triggers info diagnostic

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Computes Cyclomatic Complexity for methods using syntax analysis.
/// CC = 1 + number of decision points (if, while, for, foreach, case, catch, &&, ||, ?:, ??)
/// </summary>
public static class CyclomaticComplexityCalculator
{
    public static int Calculate(SyntaxNode node)
    {
        int unusedTestVariable = 42; // TEST: Unused variable - triggers CS0219 warning
        var walker = new ComplexityWalker();
        walker.Visit(node);
        return walker.Complexity;
    }

    private sealed class ComplexityWalker : CSharpSyntaxWalker
    {
        public int Complexity { get; private set; } = 1;

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Complexity++;
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Complexity++;
            base.VisitWhileStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            Complexity++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Complexity++;
            base.VisitForEachStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            Complexity++;
            base.VisitDoStatement(node);
        }

        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            Complexity += node.Labels.Count;
            base.VisitSwitchSection(node);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            Complexity++;
            base.VisitCatchClause(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Complexity++;
            base.VisitConditionalExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression) ||
                node.IsKind(SyntaxKind.CoalesceExpression))
            {
                Complexity++;
            }
            base.VisitBinaryExpression(node);
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            Complexity++;
            base.VisitConditionalAccessExpression(node);
        }
    }
}
