using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CDelta.Frontend.CSharp;

public static class CompilationFactory
{
    private const string GlobalUsingsPath = "<global-usings>";
    private const string MissingExternBodyId = "CS0626";

    private const string GlobalUsings = """
        global using System;
        global using System.Runtime.CompilerServices;
        global using System.Runtime.InteropServices;
        global using CDelta;
        global using static CDelta.Intrinsics;
        """;

    public static CSharpCompilation Create(IReadOnlyList<SourceInput> sources, string corelibPath, string assemblyName)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(GlobalUsings, parse, GlobalUsingsPath) };
        foreach (SourceInput source in sources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(source.Text, parse, source.FullPath));
        }

        CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(true)
            .WithOverflowChecks(false)
            .WithNullableContextOptions(NullableContextOptions.Disable)
            .WithDeterministic(true)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                [MissingExternBodyId] = ReportDiagnostic.Suppress,
            });
        return CSharpCompilation.Create(assemblyName, trees, [MetadataReference.CreateFromFile(corelibPath)], options);
    }
}
