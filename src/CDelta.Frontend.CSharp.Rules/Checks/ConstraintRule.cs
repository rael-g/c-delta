using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class ConstraintRule : SubsetRule
{
    private const string NotNull = "notnull";

    public override DiagnosticCode Code => DiagnosticCode.CD1000;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                foreach (TypeParameterConstraintSyntax constraint in ((TypeParameterConstraintClauseSyntax)node.Node).Constraints)
                {
                    if (IsUnsatisfiable(node.SemanticModel, constraint))
                    {
                        Report(node, constraint);
                    }
                }
            },
            SyntaxKind.TypeParameterConstraintClause);

    private static bool IsUnsatisfiable(SemanticModel model, TypeParameterConstraintSyntax constraint) => constraint switch
    {
        ClassOrStructConstraintSyntax { RawKind: (int)SyntaxKind.ClassConstraint } => true,
        TypeConstraintSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: NotNull } } => true,
        TypeConstraintSyntax type => model.GetTypeInfo(type.Type).Type is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Class },
        _ => false,
    };
}
