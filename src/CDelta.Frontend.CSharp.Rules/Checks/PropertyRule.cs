using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class PropertyRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1004;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var declaration = (PropertyDeclarationSyntax)node.Node;
                if (node.SemanticModel.GetDeclaredSymbol(declaration, node.CancellationToken) is not IPropertySymbol property
                    || !IsStaticAbstractGetter(property) && !ImplementsStaticAbstractGetter(property))
                {
                    Report(node, declaration);
                }
            },
            SyntaxKind.PropertyDeclaration);

    private static bool IsStaticAbstractGetter(IPropertySymbol property) =>
        property is { IsStatic: true, IsAbstract: true, ContainingType.TypeKind: TypeKind.Interface }
        && property.GetMethod is not null
        && property.SetMethod is null;

    private static bool ImplementsStaticAbstractGetter(IPropertySymbol property) =>
        property is { IsStatic: true, ContainingType.TypeKind: TypeKind.Struct }
        && property.GetMethod is not null
        && property.SetMethod is null
        && (property.ExplicitInterfaceImplementations.Length > 0
            || property.ContainingType.AllInterfaces
                .SelectMany(i => i.GetMembers().OfType<IPropertySymbol>())
                .Any(member => IsStaticAbstractGetter(member)
                    && SymbolEqualityComparer.Default.Equals(property.ContainingType.FindImplementationForInterfaceMember(member), property)));
}
