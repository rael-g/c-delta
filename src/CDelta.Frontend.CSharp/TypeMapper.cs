using CDelta.Ir;
using Microsoft.CodeAnalysis;
using TypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.CSharp;

internal sealed class TypeMapper(ModuleBuilder builder, Func<Location, SourceSpan> spanOf)
{
    private const string Language = "csharp";
    private const int SequentialLayout = 0;
    private const int ExplicitLayout = 2;
    private const int AutoLayout = 3;

    private static readonly SymbolDisplayFormat _nameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private readonly Dictionary<INamedTypeSymbol, StructId> _structs = new(SymbolEqualityComparer.Default);

    public StructId StructOf(INamedTypeSymbol type, Location location)
    {
        type = type.OriginalDefinition;
        if (_structs.TryGetValue(type, out StructId existing))
        {
            return existing;
        }

        if (type.TypeKind != Microsoft.CodeAnalysis.TypeKind.Struct || type.IsGenericType || !type.Locations.Any(l => l.IsInSource))
        {
            throw new UnsupportedConstructException("the type " + type.ToDisplayString() + " is not supported yet", location);
        }

        (StructLayoutKind layout, int pack, int minSize) = LayoutOf(type);
        Module module = builder.Module;
        StructId id = builder.BeginStruct(
            type.GetDocumentationCommentId()![2..],
            type.ToDisplayString(_nameFormat),
            spanOf(type.Locations[0]),
            Origin.FromSource(module.Intern(Language)),
            layout,
            pack,
            minSize,
            []);
        _structs.Add(type, id);

        var fields = new List<FieldDecl>();
        foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst || field.IsImplicitlyDeclared)
            {
                continue;
            }

            fields.Add(FieldOf(type, field, layout));
        }

        builder.DefineFields(id, fields);
        return id;
    }

    public int FieldIndex(INamedTypeSymbol type, IFieldSymbol field)
    {
        int index = 0;
        foreach (IFieldSymbol candidate in type.OriginalDefinition.GetMembers().OfType<IFieldSymbol>())
        {
            if (candidate.IsStatic || candidate.IsConst || candidate.IsImplicitlyDeclared)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(candidate, field.OriginalDefinition))
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException("a field is not a member of its struct");
    }

    public TypeId Map(ITypeSymbol type, Location location)
    {
        switch (type)
        {
            case IPointerTypeSymbol pointer:
                TypeId element = pointer.PointedAtType.SpecialType == SpecialType.System_Void
                    ? builder.Primitive(TypeKind.Void)
                    : Map(pointer.PointedAtType, location);
                return builder.Pointer(element, TypeFlags.Nullable | TypeFlags.Many);
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct, SpecialType: SpecialType.None } named:
                return builder.StructType(StructOf(named, location));
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

    public TypeId ParameterType(IParameterSymbol parameter, Location location) =>
        ReferenceType(Map(parameter.Type, location), parameter.RefKind);

    public TypeId ReferenceType(TypeId target, RefKind kind) => kind switch
    {
        RefKind.None => target,
        RefKind.Ref or RefKind.Out => builder.Pointer(target, TypeFlags.None),
        _ => builder.Pointer(target, TypeFlags.Const),
    };

    public static ParamMode ModeOf(RefKind kind) => kind switch
    {
        RefKind.None => ParamMode.Value,
        RefKind.Ref => ParamMode.Ref,
        RefKind.Out => ParamMode.Out,
        _ => ParamMode.RefReadonly,
    };

    private FieldDecl FieldOf(INamedTypeSymbol owner, IFieldSymbol field, StructLayoutKind layout)
    {
        Location location = field.Locations.FirstOrDefault() ?? Location.None;
        TypeId type;
        if (field.IsFixedSizeBuffer)
        {
            type = builder.ArrayOf(Map(((IPointerTypeSymbol)field.Type).PointedAtType, location), field.FixedSize);
        }
        else if (IsInlineArray(field.Type, out int length, out ITypeSymbol? element))
        {
            type = builder.ArrayOf(Map(element!, location), length);
        }
        else
        {
            type = Map(field.Type, location);
        }

        int width = FieldDecl.NoBits;
        int offset = FieldDecl.NoOffset;
        foreach (AttributeData attribute in field.GetAttributes())
        {
            string? name = attribute.AttributeClass?.ToDisplayString();
            if (name == "CDelta.BitsAttribute" && attribute.ConstructorArguments[0].Value is int bits)
            {
                width = bits;
            }
            else if (name == "System.Runtime.InteropServices.FieldOffsetAttribute" && attribute.ConstructorArguments[0].Value is int declared)
            {
                offset = declared;
            }
        }

        if (layout == StructLayoutKind.Explicit && offset == FieldDecl.NoOffset)
        {
            throw new UnsupportedConstructException("a field of an explicit layout needs [FieldOffset]", location);
        }

        return new FieldDecl(builder.Module.Intern(field.Name), type, IdRange.Empty, width, offset, FieldDecl.NoBitOffset);
    }

    private static bool IsInlineArray(ITypeSymbol type, out int length, out ITypeSymbol? element)
    {
        length = 0;
        element = null;
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute"
                && attribute.ConstructorArguments[0].Value is int count
                && type is INamedTypeSymbol named)
            {
                IFieldSymbol? only = named.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsStatic);
                if (only is not null)
                {
                    length = count;
                    element = only.Type;
                    return true;
                }
            }
        }

        return false;
    }

    private static (StructLayoutKind Layout, int Pack, int MinSize) LayoutOf(INamedTypeSymbol type)
    {
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.InteropServices.StructLayoutAttribute")
            {
                continue;
            }

            int kind = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int declared ? declared : SequentialLayout;
            int pack = 0;
            int size = 0;
            foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
            {
                if (named.Key == "Pack" && named.Value.Value is int p)
                {
                    pack = p;
                }
                else if (named.Key == "Size" && named.Value.Value is int s)
                {
                    size = s;
                }
            }

            StructLayoutKind layout = kind switch
            {
                ExplicitLayout => StructLayoutKind.Explicit,
                AutoLayout => StructLayoutKind.Auto,
                _ => pack > 0 ? StructLayoutKind.Packed : StructLayoutKind.C,
            };
            return (layout, pack, size);
        }

        return (StructLayoutKind.C, 0, 0);
    }
}
