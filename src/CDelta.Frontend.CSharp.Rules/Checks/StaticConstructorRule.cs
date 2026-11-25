using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StaticConstructorRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1009;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (Syntax.Has(node.Node, SyntaxKind.StaticKeyword))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.ConstructorDeclaration);
}
