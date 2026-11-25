using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class AllowlistRule : SubsetRule
{
    private static readonly OperationKind[] _inspected = Enum.GetValues(typeof(OperationKind))
        .Cast<OperationKind>()
        .Where(kind => kind != OperationKind.None)
        .Distinct()
        .ToArray();

    public override DiagnosticCode Code => DiagnosticCode.CD1000;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxTreeAction(tree =>
        {
            foreach (SyntaxNode node in tree.Tree.GetRoot(tree.CancellationToken).DescendantNodesAndSelf())
            {
                if (!SyntaxAllowlist.Kinds.Contains(node.Kind()))
                {
                    tree.ReportDiagnostic(Create(node.GetLocation()));
                }
            }
        });
        context.RegisterOperationAction(
            operation =>
            {
                if (!OperationAllowlist.Kinds.Contains(operation.Operation.Kind) && !operation.Operation.IsImplicit)
                {
                    Report(operation, operation.Operation);
                }
            },
            _inspected);
    }
}
