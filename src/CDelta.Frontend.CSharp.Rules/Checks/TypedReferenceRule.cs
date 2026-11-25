using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class TypedReferenceRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1027;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            node => Report(node, node.Node),
            SyntaxKind.MakeRefExpression,
            SyntaxKind.RefTypeExpression,
            SyntaxKind.RefValueExpression);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (node.SemanticModel.GetTypeInfo(node.Node).Type?.ToDisplayString() == "System.TypedReference")
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var parameter = (ParameterSyntax)node.Node;
                if (parameter.Identifier.IsKind(SyntaxKind.ArgListKeyword) && !IsExtern(node.SemanticModel, parameter))
                {
                    Report(node, parameter);
                }
            },
            SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                SyntaxNode? argument = node.Node.Parent is InvocationExpressionSyntax call ? call.Parent : node.Node.Parent;
                if (argument is ArgumentSyntax
                    && argument.Parent?.Parent is InvocationExpressionSyntax invocation
                    && node.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { IsExtern: true })
                {
                    return;
                }

                Report(node, node.Node);
            },
            SyntaxKind.ArgListExpression);
    }

    private static bool IsExtern(SemanticModel model, SyntaxNode node)
    {
        MethodDeclarationSyntax? method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        return method is not null && method.Modifiers.Any(SyntaxKind.ExternKeyword);
    }
}
