using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CDelta.Frontend.CSharp;

public static class ImportScanner
{
    private const string ImportName = "Import";

    public static List<ImportDeclaration> Scan(IReadOnlyList<SourceInput> sources)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var found = new List<ImportDeclaration>();
        foreach (SourceInput source in sources)
        {
            SyntaxNode root = CSharpSyntaxTree.ParseText(source.Text, parse, source.FullPath).GetRoot();
            foreach (ClassDeclarationSyntax declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (AttributeSyntax attribute in declaration.AttributeLists.SelectMany(list => list.Attributes))
                {
                    if (IsImport(attribute))
                    {
                        found.Add(Read(declaration, attribute, source.FullPath));
                    }
                }
            }
        }

        return found;
    }

    private static bool IsImport(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        return name is ImportName or ImportName + "Attribute" or "CDelta." + ImportName or "CDelta." + ImportName + "Attribute";
    }

    private static ImportDeclaration Read(ClassDeclarationSyntax declaration, AttributeSyntax attribute, string path)
    {
        var headers = new List<string>();
        var defines = new List<string>();
        var includes = new List<string>();
        var only = new List<string>();
        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList?.Arguments ?? [])
        {
            List<string> values = [.. argument.Expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)).Select(l => l.Token.ValueText)];
            switch (argument.NameEquals?.Name.Identifier.ValueText)
            {
                case null:
                    headers.AddRange(values);
                    break;
                case "Defines":
                    defines.AddRange(values);
                    break;
                case "IncludeDirs":
                    includes.AddRange(values);
                    break;
                case "Only":
                    only.AddRange(values);
                    break;
            }
        }

        return new ImportDeclaration(NamespaceOf(declaration), declaration.Identifier.ValueText, path, headers, defines, includes, only);
    }

    private static string NamespaceOf(SyntaxNode node)
    {
        var parts = new List<string>();
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            if (ancestor is BaseNamespaceDeclarationSyntax declaration)
            {
                parts.Insert(0, declaration.Name.ToString());
            }
        }

        return string.Join('.', parts);
    }
}
