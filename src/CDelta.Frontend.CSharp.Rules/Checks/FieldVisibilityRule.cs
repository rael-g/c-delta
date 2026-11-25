using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class FieldVisibilityRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1025;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (!Syntax.Has(node.Node, SyntaxKind.PublicKeyword) || Syntax.Has(node.Node, SyntaxKind.ProtectedKeyword)
                    || Syntax.Has(node.Node, SyntaxKind.InternalKeyword) || Syntax.Has(node.Node, SyntaxKind.PrivateKeyword))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.FieldDeclaration);
}
