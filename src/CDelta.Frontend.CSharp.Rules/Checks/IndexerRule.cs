using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class IndexerRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1005;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (IndexerDeclarationSyntax)node.Node;
                if (declaration.AccessorList?.Accessors.Any(a => a.Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration) == true)
                {
                    Report(node, declaration);
                }
            },
            SyntaxKind.IndexerDeclaration);
}
