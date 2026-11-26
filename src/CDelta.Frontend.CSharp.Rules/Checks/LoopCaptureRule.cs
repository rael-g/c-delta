using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class LoopCaptureRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD2009;

    public override void Register(AnalysisContext context) =>
        context.RegisterOperationAction(CheckAssignment, OperationKind.SimpleAssignment);

    private void CheckAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is IDiscardOperation)
        {
            return;
        }

        foreach (SyntaxNode loop in LoopsCaptured(context.Operation.SemanticModel, assignment.Value))
        {
            if (!IsInside(RootOf(assignment.Target), loop))
            {
                Report(context, assignment);
                return;
            }
        }
    }

    private static IEnumerable<SyntaxNode> LoopsCaptured(SemanticModel? model, IOperation value)
    {
        IOperation current = Unwrap(value);
        if (current is ILocalReferenceOperation { Local.DeclaringSyntaxReferences: { Length: > 0 } references } && model is not null
            && references[0].GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initializer }
            && model.GetOperation(initializer) is { } initial)
        {
            foreach (SyntaxNode loop in LoopsCaptured(model, initial))
            {
                yield return loop;
            }

            yield break;
        }

        SyntaxNode? function = current switch
        {
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation lambda } => lambda.Syntax,
            IAnonymousFunctionOperation lambda => lambda.Syntax,
            IDelegateCreationOperation { Target: IMethodReferenceOperation { Method.MethodKind: MethodKind.LocalFunction } reference }
                => reference.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(),
            _ => null,
        };
        if (model is null || function is null)
        {
            yield break;
        }

        DataFlowAnalysis? flow = function is LocalFunctionStatementSyntax local
            ? model.AnalyzeDataFlow(local)
            : model.AnalyzeDataFlow(function);
        if (flow is null || !flow.Succeeded)
        {
            yield break;
        }

        foreach (ISymbol captured in flow.ReadInside.Concat(flow.WrittenInside).Distinct(SymbolEqualityComparer.Default)
            .Where(symbol => symbol is ILocalSymbol or IParameterSymbol && !symbol.DeclaringSyntaxReferences.Any(r => function.Span.Contains(r.GetSyntax().Span))))
        {
            foreach (SyntaxReference reference in captured.DeclaringSyntaxReferences)
            {
                if (EnclosingIteration(reference.GetSyntax()) is { } loop)
                {
                    yield return loop;
                }
            }
        }
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static SyntaxNode? EnclosingIteration(SyntaxNode declaration)
    {
        for (SyntaxNode? node = declaration, child = null; node is not null; child = node, node = node.Parent)
        {
            switch (node)
            {
                case ForEachStatementSyntax forEach when child == forEach.Statement || declaration is ForEachStatementSyntax || declaration == forEach:
                    return forEach;
                case ForStatementSyntax forStatement when child == forStatement.Statement:
                    return forStatement;
                case WhileStatementSyntax whileStatement when child == whileStatement.Statement:
                    return whileStatement;
                case DoStatementSyntax doStatement when child == doStatement.Statement:
                    return doStatement;
                case BaseMethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                    return null;
            }
        }

        return null;
    }

    private static ISymbol? RootOf(IOperation target)
    {
        while (true)
        {
            switch (target)
            {
                case ILocalReferenceOperation local:
                    return local.Local;
                case IParameterReferenceOperation parameter:
                    return parameter.Parameter;
                case IFieldReferenceOperation { Field.IsStatic: false, Instance: { } instance }:
                    target = instance;
                    break;
                case IArrayElementReferenceOperation element:
                    target = element.ArrayReference;
                    break;
                case IConversionOperation conversion:
                    target = conversion.Operand;
                    break;
                default:
                    return null;
            }
        }
    }

    private static bool IsInside(ISymbol? root, SyntaxNode loop) =>
        root is ILocalSymbol local
        && local.DeclaringSyntaxReferences.Any(r => loop.Span.Contains(r.GetSyntax().Span));
}
