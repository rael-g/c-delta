using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ThreadStaticRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD2005;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var field = (FieldDeclarationSyntax)node.Node;
                if (Syntax.Has(field, SyntaxKind.StaticKeyword))
                {
                    return;
                }

                foreach (AttributeSyntax attribute in field.AttributeLists.SelectMany(l => l.Attributes))
                {
                    if (node.SemanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType?.ToDisplayString() == "System.ThreadStaticAttribute")
                    {
                        Report(node, attribute);
                    }
                }
            },
            SyntaxKind.FieldDeclaration);
}
