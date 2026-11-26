using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CDelta.Frontend.CSharp;

public static class CompilationFactory
{
    private const string GlobalUsingsPath = "<global-usings>";
    private static readonly string[] _promoted = ["CS0168", "CS0219", "CS0162", "CS8321", "CS8509", "CS8500"];
    private static readonly string[] _suppressed = ["CS0626", "CS8524"];

    private const string GlobalUsings = """
        global using System;
        global using System.Runtime.CompilerServices;
        global using System.Runtime.InteropServices;
        global using CDelta;
        global using static CDelta.Intrinsics;
        """;

    public static CSharpCompilation Create(
        IReadOnlyList<SourceInput> sources,
        string corelibPath,
        string assemblyName,
        IReadOnlyList<string> preprocessorSymbols)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: preprocessorSymbols);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(GlobalUsings, parse, GlobalUsingsPath) };
        foreach (SourceInput source in sources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(source.Text, parse, source.FullPath));
        }

        CSharpCompilationOptions options = new CSharpCompilationOptions(HasTopLevelStatements(trees) ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(true)
            .WithOverflowChecks(false)
            .WithNullableContextOptions(NullableContextOptions.Disable)
            .WithDeterministic(true)
            .WithSpecificDiagnosticOptions(DiagnosticOptions());
        return CSharpCompilation.Create(assemblyName, trees, [MetadataReference.CreateFromFile(corelibPath)], options);
    }

    private static bool HasTopLevelStatements(IReadOnlyList<SyntaxTree> trees) =>
        trees.Any(tree => tree.GetRoot().ChildNodes().Any(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax));

    private static Dictionary<string, ReportDiagnostic> DiagnosticOptions()
    {
        var options = new Dictionary<string, ReportDiagnostic>();
        foreach (string id in _promoted)
        {
            options[id] = ReportDiagnostic.Error;
        }

        foreach (string id in _suppressed)
        {
            options[id] = ReportDiagnostic.Suppress;
        }

        return options;
    }
}
