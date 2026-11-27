using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class SwitchExhaustivenessRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD2001;

    public override void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            operation =>
            {
                var statement = (ISwitchOperation)operation.Operation;
                var patterns = new List<IPatternOperation>();
                var covered = new HashSet<object>();
                bool hasDefault = false;
                foreach (ISwitchCaseOperation section in statement.Cases)
                {
                    foreach (ICaseClauseOperation clause in section.Clauses)
                    {
                        switch (clause)
                        {
                            case IDefaultCaseClauseOperation:
                                hasDefault = true;
                                break;
                            case ISingleValueCaseClauseOperation single:
                                if (single.Value.ConstantValue.HasValue)
                                {
                                    covered.Add(single.Value.ConstantValue.Value!);
                                }

                                break;
                            case IPatternCaseClauseOperation pattern when pattern.Guard is null:
                                patterns.Add(pattern.Pattern);
                                break;
                        }
                    }
                }

                Check(operation, statement.Value, patterns, covered, hasDefault, statement);
            },
            OperationKind.Switch);
        context.RegisterOperationAction(
            operation =>
            {
                var expression = (ISwitchExpressionOperation)operation.Operation;
                var patterns = new List<IPatternOperation>();
                foreach (ISwitchExpressionArmOperation arm in expression.Arms)
                {
                    if (arm.Guard is null)
                    {
                        patterns.Add(arm.Pattern);
                    }
                }

                Check(operation, expression.Value, patterns, [], false, expression);
            },
            OperationKind.SwitchExpression);
    }

    public static bool CoversUnion(Compilation compilation, ITypeSymbol union, IEnumerable<IPatternOperation> patterns)
    {
        var matched = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<IPatternOperation>(patterns);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case IDeclarationPatternOperation { MatchedType: { } declared }:
                    matched.Add(declared);
                    break;
                case ITypePatternOperation { MatchedType: { } named }:
                    matched.Add(named);
                    break;
                case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } or:
                    pending.Push(or.LeftPattern);
                    pending.Push(or.RightPattern);
                    break;
            }
        }

        return Implementers(compilation, union).All(matched.Contains);
    }

    public static List<INamedTypeSymbol> Implementers(Compilation compilation, ITypeSymbol union)
    {
        var found = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(compilation.Assembly.GlobalNamespace);
        while (pending.Count > 0)
        {
            INamespaceOrTypeSymbol container = pending.Pop();
            foreach (INamedTypeSymbol type in container.GetTypeMembers())
            {
                if (type.TypeKind == TypeKind.Struct && type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, union.OriginalDefinition)))
                {
                    found.Add(type);
                }

                pending.Push(type);
            }

            if (container is INamespaceSymbol space)
            {
                foreach (INamespaceSymbol nested in space.GetNamespaceMembers())
                {
                    pending.Push(nested);
                }
            }
        }

        return found;
    }

    private void Check(OperationAnalysisContext context, IOperation value, List<IPatternOperation> patterns, HashSet<object> covered, bool hasDefault, IOperation switchOperation)
    {
        if (value.Type is { TypeKind: TypeKind.Interface } contract && Syntax.HasAttribute(contract, "CDelta.UnionAttribute"))
        {
            if (!hasDefault && !patterns.Any(IsCatchAll) && !CoversUnion(context.Compilation, contract, patterns))
            {
                Report(context, switchOperation);
            }

            return;
        }

        if (value.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            return;
        }

        bool catchAll = hasDefault || patterns.Any(IsCatchAll);
        if (catchAll)
        {
            return;
        }

        bool exhaustive = enumType.Locations.Any(l => l.IsInSource)
            && !Syntax.HasAttribute(enumType, "System.FlagsAttribute")
            && !Syntax.HasAttribute(enumType, "CDelta.NonExhaustiveAttribute");
        if (exhaustive)
        {
            foreach (IPatternOperation pattern in patterns)
            {
                Collect(pattern, covered);
            }

            if (enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).All(f => covered.Contains(f.ConstantValue!)))
            {
                return;
            }
        }

        Report(context, switchOperation);
    }

    private static bool IsCatchAll(IPatternOperation pattern) => pattern switch
    {
        IDiscardPatternOperation => true,
        IDeclarationPatternOperation declaration => declaration.MatchesNull || declaration.NarrowedType is not null && declaration.DeclaredSymbol is not null && declaration.InputType.Equals(declaration.NarrowedType, SymbolEqualityComparer.Default),
        IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary => IsCatchAll(binary.LeftPattern) || IsCatchAll(binary.RightPattern),
        IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } binary => IsCatchAll(binary.LeftPattern) && IsCatchAll(binary.RightPattern),
        _ => false,
    };

    private static void Collect(IPatternOperation pattern, HashSet<object> covered)
    {
        switch (pattern)
        {
            case IConstantPatternOperation constant when constant.Value.ConstantValue.HasValue:
                covered.Add(constant.Value.ConstantValue.Value!);
                break;
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary:
                Collect(binary.LeftPattern, covered);
                Collect(binary.RightPattern, covered);
                break;
        }
    }
}
