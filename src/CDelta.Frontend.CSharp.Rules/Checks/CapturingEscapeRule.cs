using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class CapturingEscapeRule : SubsetRule
{
    private const int MaxDepth = 8;

    public override DiagnosticCode Code => DiagnosticCode.CD2002;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var returned = (IReturnOperation)operation.Operation;
                if (returned.ReturnedValue is not null && IsCapturing(operation.Compilation, returned.ReturnedValue, 0))
                {
                    Report(operation, returned);
                }
            },
            OperationKind.Return);
        context.RegisterOperationAction(
            operation =>
            {
                var assignment = (ISimpleAssignmentOperation)operation.Operation;
                if (LeavesFrame(assignment.Target) && IsCapturing(operation.Compilation, assignment.Value, 0))
                {
                    Report(operation, assignment);
                }
            },
            OperationKind.SimpleAssignment);
    }

    private static bool LeavesFrame(IOperation target)
    {
        switch (target)
        {
            case IFieldReferenceOperation { Field.IsStatic: true }:
                return true;
            case IFieldReferenceOperation { Instance: { } instance }:
                return LeavesFrame(instance);
            case IParameterReferenceOperation parameter:
                return parameter.Parameter.RefKind is RefKind.Ref or RefKind.Out;
            case IArrayElementReferenceOperation element:
                return LeavesFrame(element.ArrayReference);
            case IInvocationOperation or IPropertyReferenceOperation:
                return true;
            case IConversionOperation conversion:
                return LeavesFrame(conversion.Operand);
            default:
                return target.Syntax is PrefixUnaryExpressionSyntax { OperatorToken.Text: "*" };
        }
    }

    private static bool IsCapturing(Compilation compilation, IOperation operation, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        switch (operation)
        {
            case IConversionOperation conversion:
                return IsCapturing(compilation, conversion.Operand, depth + 1);
            case IParenthesizedOperation parenthesized:
                return IsCapturing(compilation, parenthesized.Operand, depth + 1);
            case IDelegateCreationOperation creation:
                return IsCapturing(compilation, creation.Target, depth + 1);
            case IAnonymousFunctionOperation lambda:
                return Captures(operation.SemanticModel, lambda.Syntax);
            case IMethodReferenceOperation { Method.MethodKind: MethodKind.LocalFunction } reference:
                return reference.Method.DeclaringSyntaxReferences.Any(r => Captures(operation.SemanticModel, r.GetSyntax()));
            case ILocalReferenceOperation local:
                return LocalInitializer(compilation, local.Local) is { } initializer && IsCapturing(compilation, initializer, depth + 1);
            case IObjectCreationOperation creation:
                return HoldsDelegate(creation.Type, 0) && creation.Arguments.Any(a => IsCapturing(compilation, a.Value, depth + 1));
            case IInvocationOperation invocation:
                return HoldsDelegate(invocation.Type, 0)
                    && (invocation.Arguments.Any(a => IsCapturing(compilation, a.Value, depth + 1))
                        || invocation.Instance is { } instance && IsCapturing(compilation, instance, depth + 1));
            default:
                return false;
        }
    }

    private static bool Captures(SemanticModel? model, SyntaxNode function)
    {
        if (model is null || function.Parent is null)
        {
            return false;
        }

        DataFlowAnalysis flow = function is LocalFunctionStatementSyntax local ? model.AnalyzeDataFlow(local) : model.AnalyzeDataFlow(function);
        return flow.Succeeded
            && flow.ReadInside.Concat(flow.WrittenInside).Any(symbol =>
                symbol is ILocalSymbol or IParameterSymbol && !symbol.DeclaringSyntaxReferences.Any(r => function.Span.Contains(r.GetSyntax().Span)));
    }

    private static bool HoldsDelegate(ITypeSymbol? type, int depth)
    {
        if (type is null || depth > MaxDepth)
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Delegate)
        {
            return true;
        }

        return type.TypeKind == TypeKind.Struct
            && type.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic && HoldsDelegate(f.Type, depth + 1));
    }

    private static IOperation? LocalInitializer(Compilation compilation, ILocalSymbol local)
    {
        foreach (SyntaxReference reference in local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is VariableDeclaratorSyntax { Initializer: { } initializer })
            {
                return compilation.GetSemanticModel(initializer.SyntaxTree).GetOperation(initializer.Value);
            }
        }

        return null;
    }
}
