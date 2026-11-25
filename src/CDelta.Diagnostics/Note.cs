namespace CDelta.Diagnostics;

public readonly struct Note(Location? location, string text)
{
    public Location? Location { get; } = location;

    public string Text { get; } = text;
}
