using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Counts Lines of Executable Code (LOC) - statements that actually execute.
/// Excludes declarations, comments, whitespace, and braces.
/// </summary>
public static class LinesOfCodeCalculator
{
    public static int Calculate(SyntaxNode node)
    {
        var walker = new ExecutableStatementWalker();
        walker.Visit(node);
        return walker.Count;
    }

    private sealed class ExecutableStatementWalker : CSharpSyntaxWalker
    {
        public int Count { get; private set; }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            Count++;
            base.VisitExpressionStatement(node);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            Count++;
            base.VisitLocalDeclarationStatement(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            Count++;
            base.VisitReturnStatement(node);
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Count++;
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Count++;
            base.VisitWhileStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            Count++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Count++;
            base.VisitForEachStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            Count++;
            base.VisitDoStatement(node);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            Count++;
            base.VisitSwitchStatement(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            Count++;
            base.VisitThrowStatement(node);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            Count++;
            base.VisitTryStatement(node);
        }

        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            Count++;
            base.VisitUsingStatement(node);
        }

        public override void VisitLockStatement(LockStatementSyntax node)
        {
            Count++;
            base.VisitLockStatement(node);
        }

        public override void VisitBreakStatement(BreakStatementSyntax node)
        {
            Count++;
            base.VisitBreakStatement(node);
        }

        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            Count++;
            base.VisitContinueStatement(node);
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            Count++;
            base.VisitYieldStatement(node);
        }
    }
}
