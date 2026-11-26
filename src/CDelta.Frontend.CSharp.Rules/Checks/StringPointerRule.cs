using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StringPointerRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1032;

    public override void Register(AnalysisContext context) =>
        context.RegisterOperationAction(
            operation =>
            {
                var conversion = (IConversionOperation)operation.Operation;
                if (conversion.Type is IPointerTypeSymbol
                    && conversion.Operand.Type?.SpecialType == SpecialType.System_String
                    && !conversion.Operand.ConstantValue.HasValue)
                {
                    Report(operation, conversion);
                }
            },
            OperationKind.Conversion);
}
