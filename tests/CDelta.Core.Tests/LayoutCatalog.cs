using CDelta.Ir;

namespace CDelta.Core.Tests;

internal sealed record CatalogField(string Type, int Bits = 0);

internal sealed record CatalogStruct(string Name, StructLayoutKind Layout, int Pack, IReadOnlyList<CatalogField> Fields);

internal static class LayoutCatalog
{
    public static readonly IReadOnlyList<CatalogStruct> Cases =
    [
        Plain("Scalars", "i8", "i32", "i8", "i64"),
        Plain("PointerAndBool", "bool", "i16", "ptr", "bool"),
        Plain("Floats", "f32", "f64", "u8", "f32"),
        Plain("Wide", "i8", "i128", "i8"),
        Plain("CTypes", "c.char", "c.long", "c.short", "c.longlong", "c.int"),
        Plain("PointerSized", "u8", "isize", "u8", "usize"),
        Plain("Arrays", "i8", "array:i16:3", "u8", "array:i64:2"),
        Plain("Nested", "i8", "struct:Scalars", "i16"),
        Plain("NestedTwice", "u8", "struct:Nested", "u8"),
        Plain("Slices", "u8", "slice:i32", "u8"),
        Packed("Packed1", 1, "i8", "i32", "i8", "i64"),
        Packed("Packed2", 2, "i8", "i32", "i8", "i64"),
        Packed("Packed4", 4, "u8", "f64", "u16"),
        Bits("BitsSmall", "u8:3", "u8:5", "u8:1"),
        Bits("BitsStraddle", "u32:3", "u32:30", "u32:3"),
        Bits("BitsMixedTypes", "u8:3", "u32:5", "u8:2"),
        Bits("BitsWithFields", "i8", "u16:4", "u16:9", "i32", "u8:7"),
        Bits("BitsWide", "u64:40", "u64:30", "u8:2"),
        Bits("BitsSigned", "i32:7", "i8:3", "i16:12", "i64:1"),
        Bits("BitsFull", "u32:32", "u32:1"),
        new("BitsPacked", StructLayoutKind.Packed, 1, [new("u8"), new("u32", 5), new("u32", 28), new("u8")]),
    ];

    private static CatalogStruct Plain(string name, params string[] types) =>
        new(name, StructLayoutKind.C, 0, [.. types.Select(t => new CatalogField(t))]);

    private static CatalogStruct Packed(string name, int pack, params string[] types) =>
        new(name, StructLayoutKind.Packed, pack, [.. types.Select(t => new CatalogField(t))]);

    private static CatalogStruct Bits(string name, params string[] specs) =>
        new(name, StructLayoutKind.C, 0, [.. specs.Select(Parse)]);

    private static CatalogField Parse(string spec)
    {
        int colon = spec.LastIndexOf(':');
        return colon > 0 && int.TryParse(spec[(colon + 1)..], out int bits) && !spec.StartsWith("array:", StringComparison.Ordinal)
            && !spec.StartsWith("slice:", StringComparison.Ordinal)
            ? new CatalogField(spec[..colon], bits)
            : new CatalogField(spec);
    }
}
