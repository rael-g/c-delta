using CDelta.Ir;
using CDelta.Tests;

namespace CDelta.Ir.Tests;

public sealed class IrPrinterTests
{
    [Fact]
    public void PrintsAModule()
    {
        string text = IrPrinter.Print(SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default));

        string path = Path.Combine(GoldenFile.AreaPath("CDelta.Ir.Tests", "Printer"), "max.ir");
        GoldenFile.AssertMatches(path, text.Replace(GoldenFile.RepositoryRoot(), "$ROOT"));
    }

    [Fact]
    public void QuotesReferencesThatAreNotBare()
    {
        Assert.Equal("Hello.Program.Main", IrPrinter.Reference("Hello.Program.Main"));
        Assert.Equal("\"Hello.Program.Count(System.Int32)\"", IrPrinter.Reference("Hello.Program.Count(System.Int32)"));
        Assert.Equal("\"fn\"", IrPrinter.Reference("fn"));
    }

    [Fact]
    public void EscapesStrings()
    {
        Assert.Equal("\"a\\\"b\\\\c\\nd\\x00\\xC3\\xA9\"", IrPrinter.Quote("a\"b\\c\nd\0é"));
    }
}
