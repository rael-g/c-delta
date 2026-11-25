using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class DeconstructRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1024;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var method = (MethodDeclarationSyntax)node.Node;
                if (method.Identifier.ValueText != "Deconstruct")
                {
                    return;
                }

                IEnumerable<ParameterSyntax> parameters = method.ParameterList.Parameters
                    .Where(p => !p.Modifiers.Any(SyntaxKind.ThisKeyword));
                if (parameters.Any() && parameters.All(p => p.Modifiers.Any(SyntaxKind.OutKeyword)))
                {
                    Report(node, method);
                }
            },
            SyntaxKind.MethodDeclaration);
}
