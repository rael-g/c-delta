using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal static class CConstants
{
    private const int HalfBits = 64;

    public static string Spell(Module module, ConstId id)
    {
        ConstNode constant = module.Constants[id.Index];
        string type = CTypes.Spell(module, constant.Type);
        switch (constant.Kind)
        {
            case ConstKind.Int:
                return Integer(module, constant, type);
            case ConstKind.Bool:
                return constant.Value != 0 ? "true" : "false";
            case ConstKind.Null:
                return "((" + type + ")0)";
            case ConstKind.Bytes:
                return "((" + type + ")" + StringLiteral(module.Bytes[constant.Bytes.Index]) + ")";
            default:
                throw new NotSupportedException("the constant kind " + constant.Kind + " is not supported at this stage");
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

    private static string Integer(Module module, ConstNode constant, string type)
    {
        TypeKind kind = module.Types[constant.Type.Index].Kind;
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
