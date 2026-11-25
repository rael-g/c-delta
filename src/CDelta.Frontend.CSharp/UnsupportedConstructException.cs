using Microsoft.CodeAnalysis;

namespace CDelta.Frontend.CSharp;

internal sealed class UnsupportedConstructException(string description, Location location) : Exception(description)
{
    public Location SourceLocation { get; } = location;
}
