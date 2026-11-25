namespace CDelta.Ir;

public readonly record struct Origin(OriginKind Kind, StringId Language, int ImportId, StringId Library, StringId CName)
{
    public static Origin FromSource(StringId language) =>
        new(OriginKind.Source, language, -1, StringId.None, StringId.None);

    public static Origin FromLibrary(StringId library, StringId cName) =>
        new(OriginKind.CLibrary, StringId.None, -1, library, cName);

    public static readonly Origin Synthetic =
        new(OriginKind.Synthetic, StringId.None, -1, StringId.None, StringId.None);
}
