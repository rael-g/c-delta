using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal static class CConstants
{
    private const int HalfBits = 64;

    public static string Spell(Module module, CNames names, ConstId id)
    {
        ConstNode constant = module.Constants[id.Index];
        string type = CTypes.Spell(module, names, constant.Type);
        switch (constant.Kind)
        {
            case ConstKind.Int:
                return Integer(module, constant, type);
            case ConstKind.Bool:
                return constant.Value != 0 ? "true" : "false";
            case ConstKind.Error:
                return "((uint16_t)" + module.ErrorNames[module.Strings[(int)constant.Value]] + ")";
            case ConstKind.Null:
                return "((" + type + ")0)";
            case ConstKind.Bytes:
                if (module.Types[constant.Type.Index].Kind == TypeKind.Slice)
                {
                    return "(" + type + ")" + Initializer(module, names, id);
                }

                return "((" + type + ")" + StringLiteral(module.Bytes[constant.Bytes.Index]) + ")";
            case ConstKind.Aggregate:
                return "(" + type + ")" + Initializer(module, names, id);
            default:
                throw new NotSupportedException("the constant kind " + constant.Kind + " is not supported at this stage");
        }
    }

    public static string Initializer(Module module, CNames names, ConstId id)
    {
        ConstNode constant = module.Constants[id.Index];
        TypeNode node = module.Types[constant.Type.Index];
        switch (constant.Kind)
        {
            case ConstKind.Bytes when node.Kind == TypeKind.Slice:
                byte[] data = module.Bytes[constant.Bytes.Index];
                string element = CTypes.Spell(module, names, new TypeId(node.A));
                return "{ (const " + element + "*)" + StringLiteral(data) + ", " + data.Length + " }";
            case ConstKind.Aggregate:
                int[] items = module.TypeLists[constant.Items.Index];
                var text = new StringBuilder("{ ");
                if (node.Kind == TypeKind.Slice)
                {
                    return "{ .ptr = " + Initializer(module, names, new ConstId(items[0])) + ", .len = "
                        + Initializer(module, names, new ConstId(items[1])) + " }";
                }

                if (node.Kind == TypeKind.Result)
                {
                    return items.Length == 1
                        ? "0"
                        : "{ .value = " + Initializer(module, names, new ConstId(items[0])) + ", .err = "
                            + Initializer(module, names, new ConstId(items[1])) + " }";
                }

                if (node.Kind == TypeKind.Optional)
                {
                    return "{ .value = " + Initializer(module, names, new ConstId(items[0])) + ", .has = "
                        + Initializer(module, names, new ConstId(items[1])) + " }";
                }

                if (node.Kind == TypeKind.Array)
                {
                    text.Append("{ ");
                    for (int i = 0; i < items.Length; i++)
                    {
                        text.Append(i > 0 ? ", " : string.Empty).Append(Initializer(module, names, new ConstId(items[i])));
                    }

                    text.Append(" }");
                }
                else
                {
                    StructDecl declaration = module.Structs[node.A];
                    string[] fieldNames = CStructWriter.FieldNames(module, declaration);
                    bool first = true;
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (module.Types[module.Fields[declaration.Fields.Start + i].Type.Index].Kind == TypeKind.Void)
                        {
                            continue;
                        }

                        text.Append(first ? string.Empty : ", ").Append('.').Append(fieldNames[i]).Append(" = ")
                            .Append(Initializer(module, names, new ConstId(items[i])));
                        first = false;
                    }
                }

                return text.Append(" }").ToString();
            case ConstKind.Null:
                return "0";
            default:
                return Spell(module, names, id);
        }
    }

    public static string StringLiteral(byte[] data)
    {
        var text = new StringBuilder("\"");
        foreach (byte b in data)
        {
            switch (b)
            {
                case (byte)'"':
                    text.Append("\\\"");
                    break;
                case (byte)'\\':
                    text.Append("\\\\");
                    break;
                case (byte)'?':
                    text.Append("\\?");
                    break;
                case >= 0x20 and < 0x7F:
                    text.Append((char)b);
                    break;
                default:
                    text.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                    break;
            }
        }

        return text.Append('"').ToString();
    }

    public static string EnumLiteral(Module module, EnumDecl declaration, Int128 value, string type) =>
        Integer(module, new ConstNode(ConstKind.Int, declaration.Underlying, value, BytesId.None, TypeListId.None), type);

    private static string Integer(Module module, ConstNode constant, string type)
    {
        TypeKind kind = IrTypes.ScalarKind(module, constant.Type);
        Int128 value = constant.Value;
        if (kind is TypeKind.I128 or TypeKind.U128)
        {
            ulong low = (ulong)(value & ulong.MaxValue);
            ulong high = (ulong)((value >> HalfBits) & ulong.MaxValue);
            string unsignedType = CTypes.Unsigned(kind);
            return "((" + type + ")(((" + unsignedType + ")" + high + "ULL << " + HalfBits + ") | " + low + "ULL))";
        }

        bool signed = IrTypes.IsSigned(kind, null);
        if (signed && value < 0)
        {
            Int128 magnitude = -value;
            if (magnitude <= int.MaxValue)
            {
                return "((" + type + ")(-" + Literal(magnitude, true) + "))";
            }

            return "((" + type + ")(-" + Literal(magnitude - 1, true) + " - 1))";
        }

        return "((" + type + ")" + Literal(value, signed) + ")";
    }

    private static string Literal(Int128 value, bool signed)
    {
        if (signed)
        {
            return value + (value > int.MaxValue ? "LL" : string.Empty);
        }

        return value + (value > uint.MaxValue ? "ULL" : "u");
    }
}
