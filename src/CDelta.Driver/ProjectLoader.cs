using System.Text.Json;
using CDelta.Diagnostics;
using CDelta.Toolchain;

namespace CDelta.Driver;

public static class ProjectLoader
{
    private const string Properties = "OutputType,OptimizeMode,PackageName,CToolchain,EmitC";
    private const string Items = "Compile,CSource,CIncludeDir,CDefine,NativeLibrary,NativeLibraryDir";

    public static ProjectPlan? Load(IProcessRunner runner, string projectPath, DiagnosticBag diagnostics)
    {
        string full = Path.GetFullPath(projectPath);
        ToolResult result = runner.Run("dotnet", ["msbuild", full, "-getProperty:" + Properties, "-getItem:" + Items, "-nologo"]);
        if (result.ExitCode != 0)
        {
            diagnostics.Add(DiagnosticCode.CD0001, new Location(projectPath, 0, 0), new Note(null, "the project could not be evaluated: " + result.StandardOutput.Trim() + result.StandardError.Trim()));
            return null;
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        JsonElement properties = document.RootElement.GetProperty("Properties");
        JsonElement items = document.RootElement.GetProperty("Items");
        string name = Property(properties, "PackageName") ?? Path.GetFileNameWithoutExtension(full);
        string directory = Path.GetDirectoryName(full)!;
        return new ProjectPlan(
            full,
            name,
            null,
            Property(properties, "OptimizeMode"),
            Property(properties, "CToolchain"),
            string.Equals(Property(properties, "EmitC"), "true", StringComparison.OrdinalIgnoreCase),
            [.. Entries(items, "Compile").Where(e => Metadata(e, "CDeltaGenerated") != "true").Select(e => Full(e, directory)).Order(StringComparer.Ordinal)],
            [.. Entries(items, "CSource").Select(e => (Full(e, directory), Metadata(e, "Flags") ?? string.Empty))],
            [.. Entries(items, "CIncludeDir").Select(e => Full(e, directory))],
            [.. Entries(items, "CDefine").Select(e => Metadata(e, "Value") is { Length: > 0 } value ? Identity(e) + "=" + value : Identity(e))],
            [.. Entries(items, "NativeLibrary").Select(e => (Identity(e), string.Equals(Metadata(e, "Linkage"), "Static", StringComparison.OrdinalIgnoreCase)))],
            [.. Entries(items, "NativeLibraryDir").Select(e => Full(e, directory))]);
    }

    private static string? Property(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out JsonElement value) && value.GetString() is { Length: > 0 } text ? text : null;

    private static IEnumerable<JsonElement> Entries(JsonElement items, string name) =>
        items.TryGetProperty(name, out JsonElement list) ? list.EnumerateArray() : [];

    private static string Identity(JsonElement entry) => entry.GetProperty("Identity").GetString()!;

    private static string Full(JsonElement entry, string directory) =>
        entry.TryGetProperty("FullPath", out JsonElement full) && full.GetString() is { Length: > 0 } path
            ? path
            : Path.GetFullPath(Identity(entry), directory);

    private static string? Metadata(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
}
