using System.Text;
using System.Text.RegularExpressions;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal sealed partial class CStructWriter(Module module, CNames names)
{
    private const string Indent = "    ";
    private const string PaddingPrefix = "cd_pad";
    private const string MinimumSizeField = "cd_min";

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

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

    public List<StructId> DependencyOrder()
    {
        var order = new List<StructId>();
        var visited = new HashSet<int>();
        foreach (StructId id in IrPrinter.OrderedStructs(module))
        {
            Visit(id, visited, order);
        }

        return order;
    }

    public string Definitions(List<StructId> order)
    {
        var text = new StringBuilder();
        foreach (StructId id in order)
        {
            text.Append(Definition(id)).Append('\n');
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
            string[] fieldNames = FieldNames(declaration);
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

    private void Visit(StructId id, HashSet<int> visited, List<StructId> order)
    {
        if (!visited.Add(id.Index))
        {
            return;
        }

        StructDecl declaration = module.Structs[id.Index];
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            VisitType(module.Fields[declaration.Fields.Start + i].Type, visited, order);
        }

        order.Add(id);
    }

    private void VisitType(TypeId type, HashSet<int> visited, List<StructId> order)
    {
        TypeNode node = module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Struct:
                Visit(new StructId(node.A), visited, order);
                break;
            case TypeKind.Array:
                VisitType(new TypeId(node.A), visited, order);
                break;
        }
    }

    private bool IsVoid(TypeId type) => module.Types[type.Index].Kind == TypeKind.Void;

    private string[] FieldNames(StructDecl declaration)
    {
        string[] result = new string[declaration.Fields.Count];
        var used = new HashSet<string>();
        for (int i = 0; i < result.Length; i++)
        {
            string candidate = module.Text(module.Fields[declaration.Fields.Start + i].Name);
            bool valid = Identifier().IsMatch(candidate) && !CKeywords.IsReserved(candidate)
                && !candidate.StartsWith(PaddingPrefix, StringComparison.Ordinal)
                && !candidate.StartsWith(MinimumSizeField, StringComparison.Ordinal)
                && !used.Contains(candidate);
            result[i] = valid ? candidate : "f" + i;
            used.Add(result[i]);
        }

        return result;
    }

    private string FieldText(FieldDecl field, string name)
    {
        TypeNode node = module.Types[field.Type.Index];
        string declaration = node.Kind == TypeKind.Array
            ? CTypes.Spell(module, names, new TypeId(node.A)) + " " + name + "[" + node.B + "]"
            : CTypes.Spell(module, names, field.Type) + " " + name;
        return field.BitWidth != FieldDecl.NoBits ? declaration + " : " + field.BitWidth : declaration;
    }

    private string Definition(StructId id)
    {
        StructDecl declaration = module.Structs[id.Index];
        string[] fieldNames = FieldNames(declaration);
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
                body.Append(inner).Append(Indent).Append("struct { ");
                if (field.Offset > 0)
                {
                    body.Append("uint8_t ").Append(PaddingPrefix).Append(i).Append('[').Append(field.Offset).Append("]; ");
                }

                body.Append(FieldText(field, fieldNames[i])).Append("; };\n");
            }

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
