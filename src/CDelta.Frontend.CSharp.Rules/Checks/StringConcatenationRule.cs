using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StringConcatenationRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1021;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var binary = (IBinaryOperation)operation.Operation;
                if (binary.OperatorKind == BinaryOperatorKind.Add
                    && binary.Type?.SpecialType == SpecialType.System_String
                    && !binary.ConstantValue.HasValue)
                {
                    Report(operation, binary);
                }
            },
            OperationKind.Binary);
        context.RegisterOperationAction(
            operation =>
            {
                var interpolated = (IInterpolatedStringOperation)operation.Operation;
                if (interpolated.Parent is not IInterpolatedStringHandlerCreationOperation
                    && !IsHandlerConversion(interpolated)
                    && !interpolated.ConstantValue.HasValue)
                {
                    Report(operation, interpolated);
                }
            },
            OperationKind.InterpolatedString);
    }

    private static bool IsHandlerConversion(IInterpolatedStringOperation operation) =>
        operation.Parent is IConversionOperation { Type.SpecialType: not SpecialType.System_String and not SpecialType.System_Object };
}
