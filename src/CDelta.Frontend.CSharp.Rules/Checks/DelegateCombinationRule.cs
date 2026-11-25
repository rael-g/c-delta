using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class DelegateCombinationRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1029;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var binary = (IBinaryOperation)operation.Operation;
                if (binary.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract
                    && binary.Type?.TypeKind == TypeKind.Delegate)
                {
                    Report(operation, binary);
                }
            },
            OperationKind.Binary);
        context.RegisterOperationAction(
            operation =>
            {
                var compound = (ICompoundAssignmentOperation)operation.Operation;
                if (compound.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract
                    && compound.Type?.TypeKind == TypeKind.Delegate)
                {
                    Report(operation, compound);
                }
            },
            OperationKind.CompoundAssignment);
    }
}
