using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class AsyncRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1012;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(node => Report(node, node.Node), SyntaxKind.AwaitExpression);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                foreach (SyntaxToken modifier in Syntax.ModifiersOf(node.Node))
                {
                    if (modifier.IsKind(SyntaxKind.AsyncKeyword))
                    {
                        Report(node, modifier);
                    }
                }
            },
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                SyntaxToken async = node.Node switch
                {
                    SimpleLambdaExpressionSyntax simple => simple.AsyncKeyword,
                    ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.AsyncKeyword,
                    AnonymousMethodExpressionSyntax anonymous => anonymous.AsyncKeyword,
                    _ => default,
                };
                if (async.IsKind(SyntaxKind.AsyncKeyword))
                {
                    Report(node, async);
                }
            },
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }
}
