using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class BitsRule : SubsetRule
{
    private const string BitsAttribute = "CDelta.BitsAttribute";

    public override DiagnosticCode Code => DiagnosticCode.CD2004;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                var field = (FieldDeclarationSyntax)node.Node;
                foreach (AttributeSyntax attribute in field.AttributeLists.SelectMany(l => l.Attributes))
                {
                    if (node.SemanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType?.ToDisplayString() != BitsAttribute)
                    {
                        continue;
                    }

                    if (!IsValid(node.SemanticModel, field, attribute))
                    {
                        Report(node, attribute);
                    }
                }
            },
            SyntaxKind.FieldDeclaration);

    private static bool IsValid(SemanticModel model, FieldDeclarationSyntax field, AttributeSyntax attribute)
    {
        ITypeSymbol? type = model.GetTypeInfo(field.Declaration.Type).Type;
        int? width = type?.SpecialType switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte => 8,
            SpecialType.System_Int16 or SpecialType.System_UInt16 => 16,
            SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
            SpecialType.System_Int64 or SpecialType.System_UInt64 => 64,
            _ => null,
        };
        if (width is null || Syntax.Has(field, SyntaxKind.StaticKeyword) || Syntax.Has(field, SyntaxKind.ConstKeyword))
        {
            return false;
        }

        ExpressionSyntax? argument = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        Optional<object?> value = argument is null ? default : model.GetConstantValue(argument);
        if (!value.HasValue || value.Value is not int bits || bits < 1 || bits > width)
        {
            return false;
        }

        return !(field.Parent is StructDeclarationSyntax declaration && IsExplicitLayout(model, declaration));
    }

    private static bool IsExplicitLayout(SemanticModel model, StructDeclarationSyntax declaration)
    {
        if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute"
                && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is int layout
                && layout == 2)
            {
                return true;
            }
        }

        return false;
    }
}
