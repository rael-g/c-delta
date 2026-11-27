using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class DelegateRules : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD5007;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var creation = (IDelegateCreationOperation)operation.Operation;
                if (creation.Target is IMethodReferenceOperation { Method.IsStatic: false, Instance.Type.TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct })
                {
                    operation.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5007), creation.Syntax.GetLocation()));
                }
            },
            OperationKind.DelegateCreation);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var parameter = (TypeParameterSyntax)node.Node;
                if (!parameter.VarianceKeyword.IsKind(SyntaxKind.None))
                {
                    node.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5003), parameter.VarianceKeyword.GetLocation()));
                }
            },
            SyntaxKind.TypeParameter);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var pointer = (FunctionPointerTypeSyntax)node.Node;
                if (pointer.CallingConvention is { ManagedOrUnmanagedKeyword.RawKind: (int)SyntaxKind.UnmanagedKeyword, UnmanagedCallingConventionList: null })
                {
                    node.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD1030), pointer.GetLocation()));
                }
            },
            SyntaxKind.FunctionPointerType);
    }
}
