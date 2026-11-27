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
                if (!Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetConversion(conversion).IsBoxing)
                {
                    return;
                }

                if (conversion.Type is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } contract)
                {
                    if (!contract.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "CDelta.UnionAttribute"))
                    {
                        operation.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5001), conversion.Syntax.GetLocation()));
                    }

                    return;
                }

                Report(operation, conversion);
            },
            OperationKind.Conversion);
    }
}
