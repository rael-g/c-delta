using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class InheritanceRule : SubsetRule
{
    private static readonly SyntaxKind[] _forbidden =
    [
        SyntaxKind.AbstractKeyword,
        SyntaxKind.VirtualKeyword,
        SyntaxKind.OverrideKeyword,
        SyntaxKind.SealedKeyword,
        SyntaxKind.ProtectedKeyword,
        SyntaxKind.NewKeyword,
    ];

    public override DiagnosticCode Code => DiagnosticCode.CD1003;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(node => Report(node, node.Node), SyntaxKind.BaseExpression);
        context.RegisterSyntaxNodeAction(
            node =>
            {
                bool inInterface = node.Node.Parent is InterfaceDeclarationSyntax;
                foreach (SyntaxToken modifier in Syntax.ModifiersOf(node.Node))
                {
                    if (!_forbidden.Contains(modifier.Kind()))
                    {
                        continue;
                    }

                    bool staticInterfaceMember = inInterface
                        && Syntax.Has(node.Node, SyntaxKind.StaticKeyword)
                        && modifier.Kind() is SyntaxKind.AbstractKeyword or SyntaxKind.VirtualKeyword;
                    if (!staticInterfaceMember)
                    {
                        Report(node, modifier);
                    }
                }
            },
            SyntaxKind.MethodDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.LocalFunctionStatement);
    }
}
