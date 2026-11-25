using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ParamsArrayRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1028;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var parameter = (ParameterSyntax)node.Node;
                if (parameter.Modifiers.Any(SyntaxKind.ParamsKeyword) && parameter.Type is ArrayTypeSyntax)
                {
                    Report(node, parameter);
                }
            },
            SyntaxKind.Parameter);
}
