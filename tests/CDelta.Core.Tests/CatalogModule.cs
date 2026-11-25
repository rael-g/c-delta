using System.Text;
using CDelta.Ir;

namespace CDelta.Core.Tests;

internal static class CatalogModule
{
    public static Module Build(IReadOnlyList<CatalogStruct> cases)
    {
        var module = new Module("Catalog");
        var builder = new ModuleBuilder(module);
        var ids = new Dictionary<string, StructId>();
        foreach (CatalogStruct entry in cases)
        {
            var fields = new List<FieldDecl>();
            for (int i = 0; i < entry.Fields.Count; i++)
            {
                fields.Add(new FieldDecl(
                    module.Intern("f" + i),
                    TypeOf(builder, ids, entry.Fields[i].Type),
                    IdRange.Empty,
                    entry.Fields[i].Bits,
                    FieldDecl.NoOffset,
                    FieldDecl.NoBitOffset));
            }

            ids[entry.Name] = builder.DeclareStruct(
                entry.Name,
                entry.Name,
                SourceSpan.None,
                Origin.Synthetic,
                fields,
                entry.Layout,
                entry.Pack,
                0,
                []);
        }

        return module;
    }

    public static string CSource(IReadOnlyList<CatalogStruct> cases, bool nativeInt128)
    {
        var text = new StringBuilder();
        foreach (CatalogStruct entry in cases)
        {
            if (entry.Layout == StructLayoutKind.Packed)
            {
                text.Append("#pragma pack(push, ").Append(entry.Pack).Append(")\n");
            }

            text.Append("struct ").Append(entry.Name).Append(" {\n");
            for (int i = 0; i < entry.Fields.Count; i++)
            {
                text.Append("  ").Append(CField(entry.Fields[i], i, nativeInt128)).Append(";\n");
            }

            text.Append("};\n");
            if (entry.Layout == StructLayoutKind.Packed)
            {
                text.Append("#pragma pack(pop)\n");
            }

            text.Append("struct ").Append(entry.Name).Append(" g_").Append(entry.Name).Append(";\n");
        }

        return text.ToString();
    }

    private static string CField(CatalogField field, int index, bool nativeInt128)
    {
        string name = "f" + index;
        string[] parts = field.Type.Split(':');
        string declaration = parts[0] switch
        {
            "array" => CType(parts[1]) + " " + name + "[" + parts[2] + "]",
            "slice" => "struct { " + CType(parts[1]) + "* p; int n; } " + name,
            "struct" => "struct " + parts[1] + " " + name,
            "i128" when !nativeInt128 => "struct { unsigned long long lo; unsigned long long hi; } " + name,
            _ => CType(parts[0]) + " " + name,
        };
        return field.Bits > 0 ? declaration + " : " + field.Bits : declaration;
    }

    private static string CType(string spec) => spec switch
    {
        "bool" => "_Bool",
        "i8" => "signed char",
        "u8" => "unsigned char",
        "i16" => "short",
        "u16" => "unsigned short",
        "i32" => "int",
        "u32" => "unsigned int",
        "i64" => "long long",
        "u64" => "unsigned long long",
        "i128" => "__int128",
        "f32" => "float",
        "f64" => "double",
        "ptr" => "void*",
        "isize" => "__INTPTR_TYPE__",
        "usize" => "__UINTPTR_TYPE__",
        "c.char" => "char",
        "c.short" => "short",
        "c.int" => "int",
        "c.long" => "long",
        "c.longlong" => "long long",
        _ => throw new ArgumentException(spec),
    };

    private static TypeId TypeOf(ModuleBuilder builder, Dictionary<string, StructId> ids, string spec)
    {
        string[] parts = spec.Split(':');
        switch (parts[0])
        {
            case "array":
                return builder.ArrayOf(TypeOf(builder, ids, parts[1]), int.Parse(parts[2]));
            case "slice":
                return builder.SliceOf(TypeOf(builder, ids, parts[1]), false);
            case "struct":
                return builder.StructType(ids[parts[1]]);
            case "ptr":
                return builder.Pointer(builder.Primitive(TypeKind.Void), TypeFlags.Nullable | TypeFlags.Many);
        }

        TypeKind kind = parts[0] switch
        {
            "bool" => TypeKind.Bool,
            "i8" => TypeKind.I8,
            "u8" => TypeKind.U8,
            "i16" => TypeKind.I16,
            "u16" => TypeKind.U16,
            "i32" => TypeKind.I32,
            "u32" => TypeKind.U32,
            "i64" => TypeKind.I64,
            "u64" => TypeKind.U64,
            "i128" => TypeKind.I128,
            "f32" => TypeKind.F32,
            "f64" => TypeKind.F64,
            "isize" => TypeKind.ISize,
            "usize" => TypeKind.USize,
            "c.char" => TypeKind.CChar,
            "c.short" => TypeKind.CShort,
            "c.int" => TypeKind.CInt,
            "c.long" => TypeKind.CLong,
            "c.longlong" => TypeKind.CLongLong,
            _ => throw new ArgumentException(spec),
        };
        return builder.Primitive(kind);
    }
}
