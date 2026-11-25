using CDelta.Ir;
using TypeKind = CDelta.Ir.TypeKind;
using Microsoft.CodeAnalysis;

namespace CDelta.Frontend.CSharp;

internal sealed class TypeMapper(ModuleBuilder builder)
{
    public TypeId Map(ITypeSymbol type, Location location)
    {
        switch (type)
        {
            case IPointerTypeSymbol pointer:
                TypeId element = pointer.PointedAtType.SpecialType == SpecialType.System_Void
                    ? builder.Primitive(TypeKind.Void)
                    : Map(pointer.PointedAtType, location);
                return builder.Pointer(element, TypeFlags.Nullable | TypeFlags.Many);
        }

        TypeKind? kind = type.SpecialType switch
        {
            SpecialType.System_Void => TypeKind.Void,
            SpecialType.System_Boolean => TypeKind.Bool,
            SpecialType.System_SByte => TypeKind.I8,
            SpecialType.System_Byte => TypeKind.U8,
            SpecialType.System_Int16 => TypeKind.I16,
            SpecialType.System_UInt16 => TypeKind.U16,
            SpecialType.System_Char => TypeKind.U16,
            SpecialType.System_Int32 => TypeKind.I32,
            SpecialType.System_UInt32 => TypeKind.U32,
            SpecialType.System_Int64 => TypeKind.I64,
            SpecialType.System_UInt64 => TypeKind.U64,
            SpecialType.System_IntPtr => TypeKind.ISize,
            SpecialType.System_UIntPtr => TypeKind.USize,
            SpecialType.System_Single => TypeKind.F32,
            SpecialType.System_Double => TypeKind.F64,
            _ => null,
        };
        if (kind is null)
        {
            throw new UnsupportedConstructException("the type " + type.ToDisplayString() + " is not supported yet", location);
        }

        return builder.Primitive(kind.Value);
    }
}
