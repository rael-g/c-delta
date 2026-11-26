using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class TopLevelArgsRule : SubsetRule
{
    private const string EntryName = "<Main>$";

    public override DiagnosticCode Code => DiagnosticCode.CD1033;

    public override void Register(AnalysisContext context) =>
        context.RegisterOperationAction(
            operation =>
            {
                if (((IParameterReferenceOperation)operation.Operation).Parameter.ContainingSymbol is IMethodSymbol { Name: EntryName })
                {
                    Report(operation, operation.Operation);
                }
            },
            OperationKind.ParameterReference);
}
