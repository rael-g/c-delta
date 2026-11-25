using CDelta.Diagnostics;
using CDelta.Driver;

namespace CDelta.Driver.Tests;

public sealed class DiagnosticFormatterTests
{
    [Fact]
    public void FormatsTheMessageAndTheNotes()
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.CD3004,
            new Location("src/Program.cs", 12, 9),
            [new Note(null, "'Close' returns 'Result'; handle it or write '_ ='")]);

        Assert.Equal(
            "src/Program.cs(12,9): error CD3004: a 'Result' is discarded\n"
            + "src/Program.cs(12,9): note: 'Close' returns 'Result'; handle it or write '_ ='",
            DiagnosticFormatter.Format(diagnostic));
    }

    [Fact]
    public void ANoteWithItsOwnLocationUsesIt()
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.CD2007,
            new Location("a.cs", 1, 2),
            [new Note(new Location("b.cs", 3, 4), "other")]);

        Assert.EndsWith("\nb.cs(3,4): note: other", DiagnosticFormatter.Format(diagnostic));
    }
}
