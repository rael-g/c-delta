using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal abstract class SubsetRule
{
    public abstract DiagnosticCode Code { get; }

    public abstract void Register(AnalysisContext context);

    protected Microsoft.CodeAnalysis.Diagnostic Create(Location location) =>
        Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(Code), location);

    protected void Report(SyntaxNodeAnalysisContext context, SyntaxNode node) =>
        context.ReportDiagnostic(Create(node.GetLocation()));

    protected void Report(SyntaxNodeAnalysisContext context, SyntaxToken token) =>
        context.ReportDiagnostic(Create(token.GetLocation()));

    protected void Report(OperationAnalysisContext context, IOperation operation) =>
        context.ReportDiagnostic(Create(operation.Syntax.GetLocation()));
}
