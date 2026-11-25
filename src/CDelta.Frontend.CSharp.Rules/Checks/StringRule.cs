using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class StringRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD1020;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                TypeSyntax? type = node.Node switch
                {
                    VariableDeclarationSyntax declaration when IsStorage(declaration) => declaration.Type,
                    ParameterSyntax parameter => parameter.Type,
                    MethodDeclarationSyntax method => method.ReturnType,
                    LocalFunctionStatementSyntax local => local.ReturnType,
                    DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.ReturnType,
                    _ => null,
                };
                if (type is null || type.IsVar && node.Node is ParameterSyntax)
                {
                    return;
                }

                if (node.SemanticModel.GetTypeInfo(type).Type?.SpecialType == SpecialType.System_String
                    && !Syntax.InAttributeClass(node.SemanticModel, node.Node))
                {
                    Report(node, type);
                }
            },
            SyntaxKind.VariableDeclaration,
            SyntaxKind.Parameter,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.DelegateDeclaration);

    private static bool IsStorage(VariableDeclarationSyntax declaration) => declaration.Parent switch
    {
        LocalDeclarationStatementSyntax local => !local.IsConst,
        FieldDeclarationSyntax field => !Syntax.Has(field, SyntaxKind.ConstKeyword),
        _ => false,
    };
}
