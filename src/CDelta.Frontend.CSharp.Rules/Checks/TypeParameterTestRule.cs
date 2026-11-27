using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class TypeParameterTestRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1000;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var test = (IIsTypeOperation)operation.Operation;
                if (test.ValueOperand.Type is ITypeParameterSymbol && IsAbstract(test.TypeOperand))
                {
                    Report(operation, test);
                }
            },
            OperationKind.IsType);
        context.RegisterOperationAction(
            operation =>
            {
                var test = (IIsPatternOperation)operation.Operation;
                ITypeSymbol? matched = test.Pattern switch
                {
                    IDeclarationPatternOperation declaration => declaration.MatchedType,
                    ITypePatternOperation type => type.MatchedType,
                    _ => null,
                };
                if (test.Value.Type is ITypeParameterSymbol && matched is not null && IsAbstract(matched))
                {
                    Report(operation, test);
                }
            },
            OperationKind.IsPattern);
    }

    private static bool IsAbstract(ITypeSymbol type) =>
        type is ITypeParameterSymbol || type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface;
}
