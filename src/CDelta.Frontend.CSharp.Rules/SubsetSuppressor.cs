using System.Collections.Immutable;
using CDelta.Frontend.CSharp.Rules.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules;

[DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
public sealed class SubsetSuppressor : DiagnosticSuppressor
{
    private const string NonExhaustiveSwitchExpression = "CS8509";

    private static readonly SuppressionDescriptor _unionSwitch = new(
        "CDS8509",
        NonExhaustiveSwitchExpression,
        "A switch over a [Union] value that covers every member is exhaustive");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [_unionSwitch];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Microsoft.CodeAnalysis.Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            SyntaxTree? tree = diagnostic.Location.SourceTree;
            if (tree is null)
            {
                continue;
            }

            SyntaxNode node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            SemanticModel model = context.GetSemanticModel(tree);
            if (node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault() is { } syntax
                && model.GetOperation(syntax, context.CancellationToken) is ISwitchExpressionOperation operation
                && operation.Value.Type is { TypeKind: TypeKind.Interface } contract
                && contract.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "CDelta.UnionAttribute")
                && SwitchExhaustivenessRule.CoversUnion(context.Compilation, contract, operation.Arms.Where(arm => arm.Guard is null).Select(arm => arm.Pattern)))
            {
                context.ReportSuppression(Suppression.Create(_unionSwitch, diagnostic));
            }
        }
    }
}
