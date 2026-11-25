using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal static class Syntax
{
    public static SyntaxTokenList ModifiersOf(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax m => m.Modifiers,
        BaseTypeDeclarationSyntax t => t.Modifiers,
        BasePropertyDeclarationSyntax p => p.Modifiers,
        BaseFieldDeclarationSyntax f => f.Modifiers,
        DelegateDeclarationSyntax d => d.Modifiers,
        LocalFunctionStatementSyntax l => l.Modifiers,
        _ => default,
    };

    public static bool Has(SyntaxNode node, SyntaxKind modifier) => ModifiersOf(node).Any(modifier);

    public static bool InAttributeClass(SemanticModel model, SyntaxNode node)
    {
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            if (ancestor is ClassDeclarationSyntax declaration
                && model.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)
            {
                return DerivesFromAttribute(symbol);
            }
        }

        return false;
    }

    public static bool DerivesFromAttribute(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.Attribute")
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsIntrinsic(IMethodSymbol method, string name) =>
        method.Name == name && method.ContainingType.ToDisplayString() == "CDelta.Intrinsics";

    public static bool HasAttribute(ISymbol symbol, string name)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == name)
            {
                return true;
            }
        }

        return false;
    }
}
