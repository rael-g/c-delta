using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StringPositionRule : SubsetRule
{
    private static readonly HashSet<string> _members = ["Length", "this[]", "IndexOf", "Substring", "Count"];

    public override DiagnosticCode Code => DiagnosticCode.CD1034;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var reference = (IPropertyReferenceOperation)operation.Operation;
                if (IsRemoved(reference.Property))
                {
                    Report(operation, reference);
                }
            },
            OperationKind.PropertyReference);
        context.RegisterOperationAction(
            operation =>
            {
                var invocation = (IInvocationOperation)operation.Operation;
                if (IsRemoved(invocation.TargetMethod))
                {
                    Report(operation, invocation);
                }
            },
            OperationKind.Invocation);
    }

    private static bool IsRemoved(ISymbol member) =>
        member.ContainingType?.SpecialType == SpecialType.System_String
        && _members.Contains(member is IPropertySymbol { IsIndexer: true } ? "this[]" : member.Name);
}
