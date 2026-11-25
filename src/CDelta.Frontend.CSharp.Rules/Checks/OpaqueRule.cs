using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class OpaqueRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD2003;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (!IsValuePosition(node.Node))
                {
                    return;
                }

                if (node.SemanticModel.GetSymbolInfo(node.Node).Symbol is INamedTypeSymbol type
                    && Syntax.HasAttribute(type, "CDelta.OpaqueAttribute"))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.IdentifierName,
            SyntaxKind.GenericName);

    private static bool IsValuePosition(SyntaxNode name) => name.Parent switch
    {
        VariableDeclarationSyntax declaration => declaration.Type == name,
        ParameterSyntax parameter => parameter.Type == name,
        MethodDeclarationSyntax method => method.ReturnType == name,
        LocalFunctionStatementSyntax local => local.ReturnType == name,
        DelegateDeclarationSyntax declaration => declaration.ReturnType == name,
        ObjectCreationExpressionSyntax creation => creation.Type == name,
        CastExpressionSyntax cast => cast.Type == name,
        DefaultExpressionSyntax @default => @default.Type == name,
        ArrayTypeSyntax array => array.ElementType == name,
        TypeArgumentListSyntax => true,
        SizeOfExpressionSyntax => true,
        _ => false,
    };
}
