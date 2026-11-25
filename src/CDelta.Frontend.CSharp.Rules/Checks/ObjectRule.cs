using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ObjectRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1017;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (node.Node is Microsoft.CodeAnalysis.CSharp.Syntax.PredefinedTypeSyntax predefined
                    && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.PredefinedType);
        context.RegisterOperationAction(
            operation =>
            {
                var conversion = (Microsoft.CodeAnalysis.Operations.IConversionOperation)operation.Operation;
                if (Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetConversion(conversion).IsBoxing)
                {
                    Report(operation, conversion);
                }
            },
            OperationKind.Conversion);
    }
}
