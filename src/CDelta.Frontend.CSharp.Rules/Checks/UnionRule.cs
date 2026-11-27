using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class UnionRule : SubsetRule
{
    private const string UnionAttribute = "CDelta.UnionAttribute";

    public override DiagnosticCode Code => DiagnosticCode.CD5002;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (StructDeclarationSyntax)node.Node;
                if (node.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                {
                    return;
                }

                if (symbol.AllInterfaces.Any(IsUnion) && (symbol.IsGenericType || symbol.IsRefLikeType))
                {
                    node.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5002), declaration.Identifier.GetLocation()));
                }
            },
            SyntaxKind.StructDeclaration);
        context.RegisterOperationAction(
            operation =>
            {
                var test = (IIsTypeOperation)operation.Operation;
                if (IsClosedInput(test.ValueOperand.Type))
                {
                    operation.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5004), test.Syntax.GetLocation()));
                }
            },
            OperationKind.IsType);
        context.RegisterOperationAction(
            operation =>
            {
                var test = (IIsPatternOperation)operation.Operation;
                if (test.Pattern is IDeclarationPatternOperation or ITypePatternOperation && IsClosedInput(test.Value.Type))
                {
                    operation.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5004), test.Syntax.GetLocation()));
                }
            },
            OperationKind.IsPattern);
        context.RegisterOperationAction(
            operation =>
            {
                var invocation = (IInvocationOperation)operation.Operation;
                if (invocation.TargetMethod is { Name: "Of", ContainingType: { Name: "Dyn", Arity: 1, ContainingNamespace.Name: "CDelta" } dyn }
                    && IsUnion(dyn.TypeArguments[0]))
                {
                    operation.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(DiagnosticCode.CD5006), invocation.Syntax.GetLocation()));
                }
            },
            OperationKind.Invocation);
    }

    private static bool IsUnion(ITypeSymbol? type) =>
        type is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } && type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == UnionAttribute);

    private static bool IsClosedInput(ITypeSymbol? type) =>
        type is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } && !IsUnion(type) || type?.SpecialType == SpecialType.System_Object;
}
