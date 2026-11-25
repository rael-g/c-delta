using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ClassRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1001;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (ClassDeclarationSyntax)node.Node;
                if (Syntax.Has(declaration, SyntaxKind.StaticKeyword))
                {
                    return;
                }

                if (node.SemanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol && Syntax.DerivesFromAttribute(symbol))
                {
                    return;
                }

                Report(node, declaration);
            },
            SyntaxKind.ClassDeclaration);
}
