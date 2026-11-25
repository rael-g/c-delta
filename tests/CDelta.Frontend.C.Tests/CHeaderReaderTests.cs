using System.Text;
using CDelta.Diagnostics;
using CDelta.Frontend.C;
using CDelta.Ir;
using CDelta.Tests;
using CDelta.Toolchain;

namespace CDelta.Frontend.C.Tests;

public sealed class CHeaderReaderTests
{
    private const string Target = "x86_64-linux-gnu";

    [Fact]
    public void ReadsTheDeclarationsOfASampleHeader()
    {
        CImportResult result = ReadSample("sample.h");
        var text = new StringBuilder(IrPrinter.Print(result.Module));
        foreach (ImportedMacro macro in result.Macros.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            text.Append("macro ").Append(macro.Name).Append(' ').Append(macro.Kind).Append(' ').Append(macro.CType).Append(' ')
                .Append(macro.Kind == MacroKind.Integer ? macro.Integer.ToString() : macro.Kind == MacroKind.Float ? macro.Float.ToString(System.Globalization.CultureInfo.InvariantCulture) : macro.Text).Append('\n');
        }

        foreach ((string key, string reason) in result.Unsupported.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            text.Append("unsupported ").Append(key).Append(": ").Append(reason).Append('\n');
        }

        GoldenFile.AssertMatches(Path.Combine(GoldenFile.AreaPath("CDelta.Frontend.C.Tests", "Headers"), "sample.txt"), text.ToString());
    }

    [Fact]
    public void ReadsTheCStandardLibraryHeaders()
    {
        var diagnostics = new DiagnosticBag();
        IReadOnlyList<string> system = new ClangProvider(new ProcessRunner(), "clang").SystemIncludeDirectories(Target);
        Assert.NotEmpty(system);
        var options = new ReadOptions(Target, system, [], []);
        CImportResult result = CHeaderReader.Read("probe", [new ImportRequest(["stdio.h"], [], [], [])], options, diagnostics);
        Assert.False(diagnostics.HasError, string.Join("\n", diagnostics.Items.Select(i => i.Code + " " + string.Join(",", i.Notes.Select(n => n.Text)))));
        Assert.True(result.Module.FunctionsByKey.ContainsKey("fopen"), "functions: " + string.Join(",", result.Module.FunctionsByKey.Keys.Take(20)) + " unsupported: " + string.Join(",", result.Unsupported.Keys.Take(10)));
        Assert.True(result.Module.FunctionsByKey.ContainsKey("printf"));
        Assert.True(result.Module.StructsByKey.ContainsKey("FILE"), "structs: " + string.Join(",", result.Module.StructsByKey.Keys) + " opaques: " + string.Join(",", result.Module.OpaquesByKey.Keys));
        Assert.Contains(result.Macros, macro => macro.Name == "EOF" && macro.Integer == -1);
        FunctionDecl printf = result.Module.Functions[result.Module.FunctionsByKey["printf"].Index];
        Assert.True(printf.Variadic);
    }

    [Fact]
    public void ReportsClangErrorsAsCd6001()
    {
        var diagnostics = new DiagnosticBag();
        CHeaderReader.Read("probe", [new ImportRequest(["missing-header.h"], [], [], [])], Options(), diagnostics);
        Assert.Contains(diagnostics.Items, item => item.Code == DiagnosticCode.CD6001);
    }

    [Fact]
    public void ReportsConflictingDefinesAsCd6009()
    {
        var diagnostics = new DiagnosticBag();
        CHeaderReader.Read(
            "probe",
            [new ImportRequest(["sample.h"], ["LEVEL=1"], [], []), new ImportRequest(["sample.h"], ["LEVEL=2"], [], [])],
            Options(),
            diagnostics);
        Assert.Contains(diagnostics.Items, item => item.Code == DiagnosticCode.CD6009);
    }

    private static CImportResult ReadSample(string header)
    {
        var diagnostics = new DiagnosticBag();
        CImportResult result = CHeaderReader.Read("probe", [new ImportRequest([header], [], [], [])], Options(), diagnostics);
        Assert.False(diagnostics.HasError, string.Join("\n", diagnostics.Items.Select(i => i.Code + " " + string.Join(",", i.Notes.Select(n => n.Text)))));
        return result;
    }

    private static ReadOptions Options() =>
        new(Target, [], [Path.Combine(GoldenFile.RepositoryRoot(), "tests", "CDelta.Frontend.C.Tests", "Headers")], []);
}
