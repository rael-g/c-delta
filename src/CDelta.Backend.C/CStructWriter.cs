using System.Text;
using System.Text.RegularExpressions;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal sealed partial class CStructWriter(Module module, CNames names)
{
    private const string Indent = "    ";
    private const string PaddingPrefix = "cd_pad";
    private const string MinimumSizeField = "cd_min";
    private const string AlignmentField = "cd_align";

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    public List<(int ArrayType, int Struct)> TypeOrder(List<StructId> structs)
    {
        var order = new List<(int ArrayType, int Struct)>();
        var visitedStructs = new HashSet<int>();
        var visitedTypes = new HashSet<int>();
        foreach (StructId id in structs)
        {
            VisitStruct(id, visitedStructs, visitedTypes, order);
        }

        for (int i = 0; i < module.Types.Count; i++)
        {
            if (module.Types[i].Kind == TypeKind.Array)
            {
                VisitType(new TypeId(i), visitedStructs, visitedTypes, order);
            }
        }

        return order;
    }

    public string ForwardDeclarations(List<StructId> order)
    {
        var text = new StringBuilder();
        foreach (StructId id in order)
        {
            string name = names.Struct(id);
            text.Append("typedef struct ").Append(name).Append(' ').Append(name).Append(";\n");
        }

        return text.ToString();
    }

    public List<StructId> DependencyOrder() => IrPrinter.OrderedStructs(module);

    public string Definitions(List<(int ArrayType, int Struct)> order)
    {
        var text = new StringBuilder();
        foreach ((int arrayType, int structIndex) in order)
        {
            if (arrayType >= 0)
            {
                text.Append(CTypes.ArrayDefinition(module, names, module.Types[arrayType]));
            }
            else
            {
                text.Append(Definition(new StructId(structIndex))).Append('\n');
            }
        }

        return text.ToString();
    }

    public string Assertions(List<StructId> order)
    {
        var text = new StringBuilder();
        foreach (StructId id in order)
        {
            StructDecl declaration = module.Structs[id.Index];
            string name = names.Struct(id);
            string display = module.Text(declaration.Name);
            text.Append("_Static_assert(sizeof(").Append(name).Append(") == ").Append(declaration.Size)
                .Append(", \"layout of ").Append(display).Append("\");\n");
            text.Append("_Static_assert(_Alignof(").Append(name).Append(") == ").Append(declaration.Alignment)
                .Append(", \"layout of ").Append(display).Append("\");\n");
            string[] fieldNames = FieldNames(module, declaration);
            for (int i = 0; i < declaration.Fields.Count; i++)
            {
                FieldDecl field = module.Fields[declaration.Fields.Start + i];
                if (field.BitWidth != FieldDecl.NoBits || IsVoid(field.Type))
                {
                    continue;
                }

                text.Append("_Static_assert(offsetof(").Append(name).Append(", ").Append(fieldNames[i]).Append(") == ")
                    .Append(field.Offset).Append(", \"layout of ").Append(display).Append('.')
                    .Append(module.Text(field.Name)).Append("\");\n");
            }
        }

        return text.ToString();
    }

    private void VisitStruct(StructId id, HashSet<int> visitedStructs, HashSet<int> visitedTypes, List<(int ArrayType, int Struct)> order)
    {
        if (!visitedStructs.Add(id.Index))
        {
            return;
        }

        StructDecl declaration = module.Structs[id.Index];
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            VisitType(module.Fields[declaration.Fields.Start + i].Type, visitedStructs, visitedTypes, order);
        }

        order.Add((-1, id.Index));
    }

    private void VisitType(TypeId type, HashSet<int> visitedStructs, HashSet<int> visitedTypes, List<(int ArrayType, int Struct)> order)
    {
        TypeNode node = module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Struct:
                VisitStruct(new StructId(node.A), visitedStructs, visitedTypes, order);
                break;
            case TypeKind.Array:
                if (visitedTypes.Add(type.Index))
                {
                    VisitType(new TypeId(node.A), visitedStructs, visitedTypes, order);
                    order.Add((type.Index, -1));
                }

                break;
        }
    }

    private bool IsVoid(TypeId type) => module.Types[type.Index].Kind == TypeKind.Void;

    public static string[] FieldNames(Module module, StructDecl declaration)
    {
        string[] result = new string[declaration.Fields.Count];
        var used = new HashSet<string>();
        for (int i = 0; i < result.Length; i++)
        {
            string candidate = module.Text(module.Fields[declaration.Fields.Start + i].Name);
            bool valid = Identifier().IsMatch(candidate) && !CKeywords.IsReserved(candidate)
                && !candidate.StartsWith(PaddingPrefix, StringComparison.Ordinal)
                && !candidate.StartsWith(MinimumSizeField, StringComparison.Ordinal)
                && !candidate.StartsWith(AlignmentField, StringComparison.Ordinal)
                && !used.Contains(candidate);
            result[i] = valid ? candidate : "f" + i;
            used.Add(result[i]);
        }

        return result;
    }

    private string FieldText(FieldDecl field, string name)
    {
        string declaration = CTypes.Spell(module, names, field.Type) + " " + name;
        return field.BitWidth != FieldDecl.NoBits ? declaration + " : " + field.BitWidth : declaration;
    }

    private string Definition(StructId id)
    {
        StructDecl declaration = module.Structs[id.Index];
        string[] fieldNames = FieldNames(module, declaration);
        var order = new List<int>();
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            if (!IsVoid(module.Fields[declaration.Fields.Start + i].Type))
            {
                order.Add(i);
            }
        }

        order.Sort((a, b) =>
        {
            int byOffset = module.Fields[declaration.Fields.Start + a].Offset.CompareTo(module.Fields[declaration.Fields.Start + b].Offset);
            return byOffset != 0 ? byOffset : a.CompareTo(b);
        });

        var body = new StringBuilder();
        string inner = declaration.MinSize > 0 ? Indent + Indent : Indent;
        if (declaration.Layout == StructLayoutKind.Explicit)
        {
            body.Append(inner).Append("union\n").Append(inner).Append("{\n");
            foreach (int i in order)
            {
                FieldDecl field = module.Fields[declaration.Fields.Start + i];
                bool misaligned = field.Offset % CLayout.AlignmentOf(module, field.Type) != 0;
                body.Append(inner).Append(Indent).Append(misaligned ? "struct __attribute__((packed)) { " : "struct { ");
                if (field.Offset > 0)
                {
                    body.Append("uint8_t ").Append(PaddingPrefix).Append(i).Append('[').Append(field.Offset).Append("]; ");
                }

                body.Append(FieldText(field, fieldNames[i])).Append("; };\n");
            }

            body.Append(inner).Append(Indent).Append("_Alignas(").Append(declaration.Alignment).Append(") uint8_t ").Append(AlignmentField).Append(";\n");
            body.Append(inner).Append("};\n");
        }
        else
        {
            foreach (int i in order)
            {
                body.Append(inner).Append(FieldText(module.Fields[declaration.Fields.Start + i], fieldNames[i])).Append(";\n");
            }
        }

        string name = names.Struct(id);
        var text = new StringBuilder();
        bool packed = declaration.Layout == StructLayoutKind.Packed;
        if (packed)
        {
            text.Append("#pragma pack(push, ").Append(declaration.Pack).Append(")\n");
        }

        text.Append("struct ").Append(name).Append('\n').Append("{\n");
        if (declaration.MinSize > 0)
        {
            text.Append(Indent).Append("union\n").Append(Indent).Append("{\n").Append(Indent).Append(Indent).Append("struct\n")
                .Append(Indent).Append(Indent).Append("{\n").Append(Indent).Append(body).Append(Indent).Append(Indent).Append("};\n")
                .Append(Indent).Append(Indent).Append("uint8_t ").Append(MinimumSizeField).Append('[').Append(declaration.MinSize).Append("];\n")
                .Append(Indent).Append("};\n");
        }
        else
        {
            text.Append(body);
        }

        text.Append("};\n");
        if (packed)
        {
            text.Append("#pragma pack(pop)\n");
        }

        return text.ToString();
    }
}
