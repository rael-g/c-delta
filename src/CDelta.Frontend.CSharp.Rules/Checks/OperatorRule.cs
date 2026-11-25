using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class OperatorRule : SubsetRule
{
    private static readonly SyntaxKind[] _removedOperators =
    [
        SyntaxKind.TrueKeyword,
        SyntaxKind.FalseKeyword,
        SyntaxKind.PlusPlusToken,
        SyntaxKind.MinusMinusToken,
        SyntaxKind.PlusEqualsToken,
        SyntaxKind.MinusEqualsToken,
        SyntaxKind.AsteriskEqualsToken,
        SyntaxKind.SlashEqualsToken,
        SyntaxKind.PercentEqualsToken,
        SyntaxKind.AmpersandEqualsToken,
        SyntaxKind.BarEqualsToken,
        SyntaxKind.CaretEqualsToken,
        SyntaxKind.LessThanLessThanEqualsToken,
        SyntaxKind.GreaterThanGreaterThanEqualsToken,
        SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken,
    ];

    public override DiagnosticCode Code => DiagnosticCode.CD1007;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (OperatorDeclarationSyntax)node.Node;
                if (_removedOperators.Contains(declaration.OperatorToken.Kind()))
                {
                    Report(node, declaration);
                }
            },
            SyntaxKind.OperatorDeclaration);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (ConversionOperatorDeclarationSyntax)node.Node;
                if (declaration.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword))
                {
                    Report(node, declaration);
                }
            },
            SyntaxKind.ConversionOperatorDeclaration);
    }
}
