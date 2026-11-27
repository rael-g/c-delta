using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ErrorRule : SubsetRule
{
    private const string ResultType = "CDelta.Result";

    public override DiagnosticCode Code => DiagnosticCode.CD3001;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(CheckInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(CheckDiscard, OperationKind.ExpressionStatement);
        context.RegisterSymbolAction(CheckMain, SymbolKind.Method);
        context.RegisterCompilationStartAction(start =>
        {
            var handlers = new List<Location>();
            var reported = new HashSet<Location>();
            start.RegisterSymbolAction(
                symbol =>
                {
                    var method = (IMethodSymbol)symbol.Symbol;
                    if (!Syntax.HasAttribute(method, "CDelta.PanicHandlerAttribute"))
                    {
                        return;
                    }

                    lock (handlers)
                    {
                        handlers.Add(method.Locations[0]);
                    }

                    if (!HasHandlerSignature(method) || ReturnsNormally(symbol.Compilation, method))
                    {
                        lock (handlers)
                        {
                            reported.Add(method.Locations[0]);
                        }

                        symbol.ReportDiagnostic(Diagnostic(DiagnosticCode.CD3008, method.Locations[0]));
                    }
                },
                SymbolKind.Method);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (Location extra in handlers.OrderBy(l => l.SourceSpan.Start).Skip(1).Where(l => !reported.Contains(l)))
                {
                    end.ReportDiagnostic(Diagnostic(DiagnosticCode.CD3008, extra));
                }
            });
        });
    }

    private static Microsoft.CodeAnalysis.Diagnostic Diagnostic(DiagnosticCode code, Location location) =>
        Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(code), location);

    private static bool IsResult(ITypeSymbol? type) =>
        type is INamedTypeSymbol { ContainingNamespace: { Name: "CDelta", ContainingNamespace.IsGlobalNamespace: true } } named
        && named.Name == "Result";

    private static bool IsIntrinsic(IMethodSymbol method, params string[] names) =>
        names.Any(name => Syntax.IsIntrinsic(method, name));

    private static ITypeSymbol? ReturnTypeOf(ISymbol? symbol) => (symbol as IMethodSymbol)?.ReturnType;

    private static bool InDeferredBody(IOperation operation)
    {
        for (IOperation? parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation
                && parent.Parent is IDelegateCreationOperation
                && parent.Parent.Parent is IArgumentOperation { Parent: IInvocationOperation deferred }
                && IsIntrinsic(deferred.TargetMethod, "Defer", "ErrDefer"))
            {
                return true;
            }
        }

        return false;
    }

    private void CheckInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol target = invocation.TargetMethod;
        ISymbol? enclosing = EnclosingMethod(context, invocation);
        if (IsIntrinsic(target, "Try"))
        {
            if (InDeferredBody(invocation))
            {
                Report(context, invocation, DiagnosticCode.CD3009);
            }
            else if (!IsResult(ReturnTypeOf(enclosing)))
            {
                Report(context, invocation, DiagnosticCode.CD3001);
            }
            else if (!SetsAgree(invocation, ReturnTypeOf(enclosing)!))
            {
                Report(context, invocation, DiagnosticCode.CD3002);
            }
        }
        else if (IsIntrinsic(target, "ErrDefer", "Defer"))
        {
            if (invocation.Arguments.Length == 1 && !IsLambdaInPlace(invocation.Arguments[0].Value))
            {
                Report(context, invocation, DiagnosticCode.CD3005);
            }
            else if (target.Name == "ErrDefer" && !IsResult(ReturnTypeOf(EnclosingMethod(context, invocation))))
            {
                Report(context, invocation, DiagnosticCode.CD3003);
            }
        }
        else if (IsIntrinsic(target, "Panic", "Unreachable") && !IsThrown(invocation))
        {
            Report(context, invocation, DiagnosticCode.CD3007);
        }
    }

    private static ISymbol? EnclosingMethod(OperationAnalysisContext context, IOperation operation)
    {
        for (IOperation? parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ILocalFunctionOperation local)
            {
                return local.Symbol;
            }
        }

        return context.ContainingSymbol;
    }

    private static bool IsLambdaInPlace(IOperation argument)
    {
        while (argument is IConversionOperation conversion)
        {
            argument = conversion.Operand;
        }

        return argument is IDelegateCreationOperation { Target: IAnonymousFunctionOperation };
    }

    private static bool IsThrown(IOperation invocation)
    {
        IOperation? parent = invocation.Parent;
        while (parent is IConversionOperation or IParenthesizedOperation)
        {
            parent = parent.Parent;
        }

        return parent is IThrowOperation;
    }

    private static bool SetsAgree(IInvocationOperation @try, ITypeSymbol returned)
    {
        if (returned is not INamedTypeSymbol { TypeArguments.Length: 2 } typed)
        {
            return true;
        }

        ITypeSymbol? operand = @try.Arguments[0].Value.Type;
        if (operand is not INamedTypeSymbol { TypeArguments.Length: 2 } source)
        {
            return false;
        }

        HashSet<string> allowed = MemberNames(typed.TypeArguments[1]);
        return MemberNames(source.TypeArguments[1]).IsSubsetOf(allowed);
    }

    private static HashSet<string> MemberNames(ITypeSymbol set) =>
        [.. set.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsConst).Select(f => f.Name)];

    private void Report(OperationAnalysisContext context, IOperation operation, DiagnosticCode code) =>
        context.ReportDiagnostic(Diagnostic(code, operation.Syntax.GetLocation()));

    private void CheckDiscard(OperationAnalysisContext context)
    {
        var statement = (IExpressionStatementOperation)context.Operation;
        IOperation value = statement.Operation;
        if (value is IAssignmentOperation or IIncrementOrDecrementOperation)
        {
            return;
        }

        if (IsResult(value.Type))
        {
            Report(context, statement, DiagnosticCode.CD3004);
        }
    }

    private static void CheckMain(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method is not { Name: "Main", IsStatic: true, MethodKind: MethodKind.Ordinary })
        {
            return;
        }

        if (method.Parameters.Length != 0 || method.TypeParameters.Length != 0 || !IsMainReturn(method.ReturnType))
        {
            context.ReportDiagnostic(Diagnostic(DiagnosticCode.CD3006, method.Locations[0]));
        }
    }

    private static bool IsMainReturn(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_Void or SpecialType.System_Int32 } => true,
        INamedTypeSymbol { Name: "Result", TypeArguments.Length: 0 } named => IsResult(named),
        INamedTypeSymbol { Name: "Result", TypeArguments.Length: 1 } named => IsResult(named) && named.TypeArguments[0].SpecialType == SpecialType.System_Int32,
        _ => false,
    };

    private static bool HasHandlerSignature(IMethodSymbol method) =>
        method.IsStatic
        && method.ReturnsVoid
        && method.Parameters.Length == 3
        && IsByteSpan(method.Parameters[0].Type)
        && IsByteSpan(method.Parameters[1].Type)
        && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32;

    private static bool IsByteSpan(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments.Length: 1 } named
        && named.TypeArguments[0].SpecialType == SpecialType.System_Byte;

    private static bool ReturnsNormally(Compilation compilation, IMethodSymbol method)
    {
        foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not MethodDeclarationSyntax { Body: { } body })
            {
                continue;
            }

            ControlFlowAnalysis? flow = compilation.GetSemanticModel(body.SyntaxTree).AnalyzeControlFlow(body);
            if (flow is { Succeeded: true } && (flow.EndPointIsReachable || flow.ReturnStatements.Length > 0))
            {
                return true;
            }
        }

        return false;
    }

    private void Report(SymbolAnalysisContext context, Location location, DiagnosticCode code) =>
        context.ReportDiagnostic(Diagnostic(code, location));
}
