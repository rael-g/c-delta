namespace CDelta.Diagnostics;

public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = [];

    public IReadOnlyList<Diagnostic> Items => _items;

    public bool HasError => _items.Count > 0;

    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);

    public void Add(DiagnosticCode code, Location location, params Note[] notes) =>
        _items.Add(new Diagnostic(code, location, notes));
}
