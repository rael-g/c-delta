using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class SurrogateRule : SubsetRule
{
    public override DiagnosticCode Code => DiagnosticCode.CD2007;

    public override void Register(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(
            node =>
            {
                if (node.SemanticModel.GetConstantValue(node.Node).Value is string text && HasLoneSurrogate(text))
                {
                    Report(node, node.Node);
                }
            },
            SyntaxKind.StringLiteralExpression);

    private static bool HasLoneSurrogate(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
}
