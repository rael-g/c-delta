using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class DirectiveRule : SubsetRule
{
    private static readonly HashSet<string> _buildSymbols =
    [
        "OS_LINUX", "OS_WINDOWS", "OS_MACOS", "OS_FREEBSD", "OS_WASI", "OS_FREESTANDING",
        "ARCH_X86_64", "ARCH_AARCH64", "ARCH_X86", "ARCH_ARM", "ARCH_RISCV64", "ARCH_WASM32",
        "ABI_GNU", "ABI_MUSL", "ABI_MSVC",
        "MODE_DEBUG", "MODE_RELEASE_SAFE", "MODE_RELEASE_FAST", "MODE_RELEASE_SMALL",
        "LINK_LIBC", "TEST",
    ];

    public override DiagnosticCode Code => DiagnosticCode.CD1023;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxTreeAction(tree =>
        {
            foreach (SyntaxNode node in tree.Tree.GetRoot(tree.CancellationToken).DescendantNodes(descendIntoTrivia: true))
            {
                switch (node)
                {
                    case DefineDirectiveTriviaSyntax or UndefDirectiveTriviaSyntax:
                        tree.ReportDiagnostic(Create(node.GetLocation()));
                        break;
                    case IfDirectiveTriviaSyntax conditional:
                        Check(tree, conditional.Condition);
                        break;
                    case ElifDirectiveTriviaSyntax elif:
                        Check(tree, elif.Condition);
                        break;
                }
            }
        });

    private void Check(SyntaxTreeAnalysisContext context, ExpressionSyntax condition)
    {
        foreach (IdentifierNameSyntax name in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!_buildSymbols.Contains(name.Identifier.ValueText))
            {
                context.ReportDiagnostic(Create(name.GetLocation()));
            }
        }
    }
}
