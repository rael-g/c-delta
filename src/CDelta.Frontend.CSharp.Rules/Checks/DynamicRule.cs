using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class DynamicRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1016;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var name = (IdentifierNameSyntax)node.Node;
                if (name.Identifier.ValueText == "dynamic"
                    && node.SemanticModel.GetTypeInfo(name).Type?.TypeKind == TypeKind.Dynamic)
                {
                    Report(node, name);
                }
            },
            SyntaxKind.IdentifierName);
}
