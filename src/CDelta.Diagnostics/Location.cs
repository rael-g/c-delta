namespace CDelta.Diagnostics;

public readonly struct Location(string path, int line, int column)
{
    public string Path { get; } = path;

    public int Line { get; } = line;

    public int Column { get; } = column;
}
