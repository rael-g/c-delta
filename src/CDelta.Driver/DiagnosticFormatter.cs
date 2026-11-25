using System.Text;
using CDelta.Diagnostics;

namespace CDelta.Driver;

public static class DiagnosticFormatter
{
    public static string Format(Diagnostic diagnostic)
    {
        var text = new StringBuilder();
        text.Append(Where(diagnostic.Location)).Append(": error ").Append(diagnostic.Code).Append(": ")
            .Append(DiagnosticRegistry.Messages[diagnostic.Code].Replace('`', '\''));
        foreach (Note note in diagnostic.Notes)
        {
            text.Append('\n').Append(Where(note.Location ?? diagnostic.Location)).Append(": note: ").Append(note.Text);
        }

        return text.ToString();
    }

    private static string Where(Location location) => location.Path + "(" + location.Line + "," + location.Column + ")";
}
