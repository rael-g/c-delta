using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ThrowRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1011;

    public override void Register(AnalysisContext context) =>
        context.RegisterOperationAction(
            operation =>
            {
                var thrown = (IThrowOperation)operation.Operation;
                IOperation? value = thrown.Exception;
                while (value is IConversionOperation conversion)
                {
                    value = conversion.Operand;
                }

                bool allowed = value is IInvocationOperation invocation
                    && (Syntax.IsIntrinsic(invocation.TargetMethod, "Panic") || Syntax.IsIntrinsic(invocation.TargetMethod, "Unreachable"));
                if (!allowed)
                {
                    Report(operation, thrown);
                }
            },
            OperationKind.Throw);
}
