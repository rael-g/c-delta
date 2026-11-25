using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StackEscapeRule : SubsetRule
{
    private const int MaxDepth = 8;

    public override DiagnosticCode Code => DiagnosticCode.CD2006;

    public override void Register(AnalysisContext context) =>
        context.RegisterOperationAction(
            operation =>
            {
                var returned = (IReturnOperation)operation.Operation;
                if (returned.ReturnedValue is not null && IsStackMemory(operation.Compilation, returned.ReturnedValue, 0))
                {
                    Report(operation, returned);
                }
            },
            OperationKind.Return);

    private static bool IsStackMemory(Compilation compilation, IOperation operation, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        switch (operation)
        {
            case IConversionOperation conversion:
                return IsStackMemory(compilation, conversion.Operand, depth + 1);
            case IParenthesizedOperation parenthesized:
                return IsStackMemory(compilation, parenthesized.Operand, depth + 1);
            case IAddressOfOperation address:
                return address.Reference is ILocalReferenceOperation or IParameterReferenceOperation;
            case ILocalReferenceOperation local:
                return LocalInitializer(compilation, local.Local) is IOperation initializer
                    && IsStackMemory(compilation, initializer, depth + 1);
            case IObjectCreationOperation creation:
                return creation.Arguments.Any(a => IsStackMemory(compilation, a.Value, depth + 1));
            default:
                return operation.Syntax is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax;
        }
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
