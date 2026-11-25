using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ArrayRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1019;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (Allowed(node.SemanticModel, node.Node))
                {
                    return;
                }

                Report(node, node.Node);
            },
            SyntaxKind.ArrayType,
            SyntaxKind.ArrayCreationExpression,
            SyntaxKind.ImplicitArrayCreationExpression);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                ITypeSymbol? target = node.SemanticModel.GetTypeInfo(node.Node).ConvertedType;
                if (target?.TypeKind == TypeKind.Array && !Allowed(node.SemanticModel, node.Node))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.CollectionExpression);
    }

    private static bool Allowed(SemanticModel model, SyntaxNode node)
    {
        if (node.Parent is ParameterSyntax parameter && parameter.Modifiers.Any(SyntaxKind.ParamsKeyword))
        {
            return true;
        }

        if (node is ArrayTypeSyntax && node.Parent is ArrayCreationExpressionSyntax)
        {
            return true;
        }

        if (node.Parent is StackAllocArrayCreationExpressionSyntax)
        {
            return true;
        }

        if (Syntax.InAttributeClass(model, node))
        {
            return true;
        }

        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            if (ancestor is AttributeSyntax)
            {
                return true;
            }

            if (ancestor is FieldDeclarationSyntax field)
            {
                return Syntax.Has(field, SyntaxKind.StaticKeyword)
                    && Syntax.Has(field, SyntaxKind.ReadOnlyKeyword)
                    && field.Declaration.Variables.All(v => v.Initializer is not null);
            }

            if (ancestor is MemberDeclarationSyntax or StatementSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
