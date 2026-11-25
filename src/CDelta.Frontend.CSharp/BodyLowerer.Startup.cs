using CDelta.Ir;
using Microsoft.CodeAnalysis;
using TypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.CSharp;

internal sealed partial class BodyLowerer
{
    public const string ArgumentCountKey = "synthetic:argc";
    public const string ArgumentValuesKey = "synthetic:argv";

    private OperandId LowerStartup(IMethodSymbol target, SourceSpan span)
    {
        TypeId cInt = _builder.Primitive(TypeKind.CInt);
        TypeId cChar = _builder.Primitive(TypeKind.CChar);
        TypeId argv = _builder.Pointer(_builder.Pointer(cChar, TypeFlags.Nullable | TypeFlags.Many), TypeFlags.Nullable | TypeFlags.Many);
        if (target.Name == "ArgCount")
        {
            OperandId count = _f.Copy(_f.GlobalPlace(SyntheticGlobal(ArgumentCountKey, cInt, _builder.IntConstant(cInt, 0))));
            TypeId i32 = _builder.Primitive(TypeKind.I32);
            return Emit(i32, _f.Cast(CastKind.IntInt, ArithMode.Wrap, count, i32), span);
        }

        OperandId values = _f.Copy(_f.GlobalPlace(SyntheticGlobal(ArgumentValuesKey, argv, _builder.NullConstant(argv))));
        TypeId bytes = _builder.Pointer(_builder.Pointer(_builder.Primitive(TypeKind.U8), TypeFlags.Nullable | TypeFlags.Many), TypeFlags.Nullable | TypeFlags.Many);
        return Emit(bytes, _f.Cast(CastKind.PtrPtr, ArithMode.Default, values, bytes), span);
    }

    private GlobalId SyntheticGlobal(string key, TypeId type, ConstId initializer)
    {
        if (_module.GlobalsByKey.TryGetValue(key, out GlobalId existing))
        {
            return existing;
        }

        return _builder.DeclareGlobal(key, key, SourceSpan.None, Origin.Synthetic, type, true, false, initializer, LinkageKind.Internal, null);
    }
}
