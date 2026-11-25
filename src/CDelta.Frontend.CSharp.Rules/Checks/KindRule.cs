using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class KindRule(DiagnosticCode code, params SyntaxKind[] kinds) : SubsetRule
{
    public override DiagnosticCode Code => code;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(node => Report(node, node.Node), kinds);
}
