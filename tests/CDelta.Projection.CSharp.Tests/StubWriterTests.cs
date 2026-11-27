using CDelta.Projection.CSharp;
using CDelta.Diagnostics;
using CDelta.Frontend.C;
using CDelta.Tests;

namespace CDelta.Projection.CSharp.Tests;

public sealed class StubWriterTests
{
    [Fact]
    public void WritesTheStubOfASampleHeader()
    {
        GoldenFile.AssertMatches(Golden("sample.cs"), Stub([]));
    }

    [Fact]
    public void OnlyKeepsTheNamedPrefixesAndTheTypesTheyNeed()
    {
        GoldenFile.AssertMatches(Golden("sample-only.cs"), Stub(["sample_open", "SAMPLE_OK"]));
    }

    private static string Golden(string name) => Path.Combine(GoldenFile.AreaPath("CDelta.Projection.CSharp.Tests", "Stubs"), name);

    private static string Stub(string[] only)
    {
        var diagnostics = new DiagnosticBag();
        string headers = Path.Combine(AppContext.BaseDirectory, "Headers");
        CImportResult result = CHeaderReader.Read("probe", [new ImportRequest(["sample.h"], [], [], [])], new ReadOptions("x86_64-linux-gnu", [], [headers], []), diagnostics);
        Assert.False(diagnostics.HasError);
        List<StubMacro> macros = [.. result.Macros.Select(m => new StubMacro(m.Name, (StubMacroKind)(int)m.Kind, m.CType, m.Integer, m.Float, m.Text))];
        return StubWriter.Write(result.Module, macros, result.Unsupported, new StubOptions(string.Empty, "Sample", only));
    }
}
