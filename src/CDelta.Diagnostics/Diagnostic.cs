namespace CDelta.Diagnostics;

public readonly struct Diagnostic(DiagnosticCode code, Location location, IReadOnlyList<Note> notes)
{
    public DiagnosticCode Code { get; } = code;

    public Location Location { get; } = location;

    public IReadOnlyList<Note> Notes { get; } = notes;
}
