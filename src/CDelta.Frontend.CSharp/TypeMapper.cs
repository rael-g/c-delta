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
    private readonly Dictionary<INamedTypeSymbol, EnumId> _enums = new(SymbolEqualityComparer.Default);

    public Func<INamedTypeSymbol?, int> ImportIdOf { get; set; } = _ => -1;

    public Func<INamedTypeSymbol, IReadOnlyList<INamedTypeSymbol>> ImplementersOf { get; set; } = _ => [];

    private readonly Dictionary<INamedTypeSymbol, UnionId> _unions = new(SymbolEqualityComparer.Default);

    public Action<INamedTypeSymbol, StructId> StructDefined { get; set; } = (_, _) => { };

    private readonly Dictionary<INamedTypeSymbol, InterfaceId> _interfaces = new(SymbolEqualityComparer.Default);

    private readonly Dictionary<INamedTypeSymbol, TypeId> _opaques = new(SymbolEqualityComparer.Default);

    private Origin OriginOf(INamedTypeSymbol type)
    {
        int import = ImportIdOf(type.ContainingType);
        return import < 0
            ? Origin.FromSource(builder.Module.Intern(Language))
            : new Origin(OriginKind.CHeader, StringId.None, import, StringId.None, builder.Module.Intern(CSharpFrontend.CNameOf(type)));
    }

    private static bool IsOpaque(INamedTypeSymbol type) =>
        type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "CDelta.OpaqueAttribute");

    private TypeId OpaqueOf(INamedTypeSymbol type, Location location)
    {
        type = type.OriginalDefinition;
        if (_opaques.TryGetValue(type, out TypeId existing))
        {
            return existing;
        }

        TypeId id = builder.OpaqueType(builder.DeclareOpaque(
            type.GetDocumentationCommentId()![2..],
            type.ToDisplayString(_nameFormat),
            spanOf(location),
            OriginOf(type)));
        _opaques[type] = id;
        return id;
    }

    public StructId StructOf(INamedTypeSymbol type, Location location)
    {
        type = type.OriginalDefinition;
        if (_structs.TryGetValue(type, out StructId existing))
        {
            return existing;
        }

        if (type.TypeKind != Microsoft.CodeAnalysis.TypeKind.Struct || !type.Locations.Any(l => l.IsInSource))
        {
            throw new UnsupportedConstructException("the type " + type.ToDisplayString() + " is not supported yet", location);
        }

        (StructLayoutKind layout, int pack, int minSize) = LayoutOf(type);
        Module module = builder.Module;
        StructId id = builder.BeginStruct(
            type.GetDocumentationCommentId()![2..],
            type.ToDisplayString(_nameFormat),
            spanOf(type.Locations[0]),
            OriginOf(type),
            layout,
            pack,
            minSize,
            []);
        _structs.Add(type, id);
        builder.SetStructTypeParams(id, [.. type.TypeParameters.Select(parameter => parameter.Name)]);

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
        StructDefined(type, id);
        return id;
    }

    public InterfaceId InterfaceOf(INamedTypeSymbol type, Location location)
    {
        type = type.OriginalDefinition;
        if (_interfaces.TryGetValue(type, out InterfaceId existing))
        {
            return existing;
        }

        InterfaceId id = builder.DeclareInterface(
            type.GetDocumentationCommentId()![2..],
            type.ToDisplayString(_nameFormat),
            spanOf(type.Locations.FirstOrDefault() ?? Location.None),
            [.. type.TypeParameters.Select(parameter => parameter.Name)]);
        _interfaces.Add(type, id);

        var methods = new List<InterfaceMethod>();
        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!IsContractMember(method))
            {
                continue;
            }

            if (method.IsGenericMethod)
            {
                throw new UnsupportedConstructException("a generic interface method is not supported yet", location);
            }

            var parameters = method.Parameters
                .Select(parameter => new SigParam(Map(parameter.Type, location), ModeOf(parameter.RefKind)))
                .ToList();
            SigId sig = builder.Signature(CallConv.Default, parameters, ReferenceType(Map(method.ReturnType, location), method.RefKind), false, ModeOf(method.RefKind));
            methods.Add(new InterfaceMethod(builder.Module.Intern(method.Name), method.IsStatic, sig, FunctionId.None));
        }

        builder.DefineInterfaceMethods(id, methods);
        return id;
    }

    public TypeId InterfaceType(INamedTypeSymbol contract, Location location) =>
        builder.InterfaceType(InterfaceOf(contract, location), [.. contract.TypeArguments.Select(argument => Map(argument, location))]);

    public SigId DelegateSignature(INamedTypeSymbol delegateType, Location location)
    {
        IMethodSymbol invoke = delegateType.DelegateInvokeMethod!;
        var parameters = invoke.Parameters
            .Select(parameter => new SigParam(ParameterType(parameter, location), ModeOf(parameter.RefKind)))
            .ToList();
        return builder.Signature(CallConv.Default, parameters, ReferenceType(Map(invoke.ReturnType, location), invoke.RefKind), false, ModeOf(invoke.RefKind));
    }

    public static CallConv ConventionOf(IMethodSymbol signature)
    {
        foreach (INamedTypeSymbol convention in signature.UnmanagedCallingConventionTypes)
        {
            switch (convention.Name)
            {
                case "CallConvCdecl":
                    return CallConv.Cdecl;
                case "CallConvStdcall":
                    return CallConv.Stdcall;
                case "CallConvFastcall":
                    return CallConv.Fastcall;
                case "CallConvThiscall":
                    return CallConv.Thiscall;
            }
        }

        return CallConv.Default;
    }

    public static bool IsUnion(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } contract
        && contract.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "CDelta.UnionAttribute");

    public UnionId UnionOf(INamedTypeSymbol contract, Location location)
    {
        contract = contract.OriginalDefinition;
        if (_unions.TryGetValue(contract, out UnionId existing))
        {
            return existing;
        }

        UnionId id = builder.DeclareUnion(
            contract.GetDocumentationCommentId()![2..],
            contract.ToDisplayString(_nameFormat),
            spanOf(contract.Locations.FirstOrDefault() ?? Location.None),
            InterfaceType(contract, location));
        _unions.Add(contract, id);
        builder.DefineUnionMembers(id, [.. ImplementersOf(contract).Select(member => Map(member, location))]);
        return id;
    }

    public int UnionMemberIndex(INamedTypeSymbol contract, ITypeSymbol member, Location location)
    {
        UnionDecl declaration = builder.Module.Unions[UnionOf(contract, location).Index];
        TypeId type = Map(member, location);
        for (int i = 0; i < declaration.Members.Count; i++)
        {
            if (builder.Module.UnionMembers[declaration.Members.Start + i] == type)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool IsContractMember(IMethodSymbol member) =>
        member.MethodKind is MethodKind.Ordinary or MethodKind.PropertyGet or MethodKind.UserDefinedOperator;

    public static int ContractMethodIndex(IMethodSymbol method)
    {
        int index = 0;
        foreach (IMethodSymbol member in method.ContainingType.OriginalDefinition.GetMembers().OfType<IMethodSymbol>().Where(IsContractMember))
        {
            if (SymbolEqualityComparer.Default.Equals(member, method.OriginalDefinition))
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException("a method is not a member of its interface");
    }

    public StructId TupleOf(INamedTypeSymbol tuple, Location location)
    {
        var elements = new List<TypeId>();
        foreach (IFieldSymbol element in tuple.TupleElements)
        {
            elements.Add(Map(element.Type, location));
        }

        Module module = builder.Module;
        string key = "tuple<" + string.Join(",", elements.Select(e => IrPrinter.TypeText(module, e))) + ">";
        if (module.StructsByKey.TryGetValue(key, out StructId existing))
        {
            return existing;
        }

        var fields = new List<FieldDecl>();
        for (int i = 0; i < elements.Count; i++)
        {
            fields.Add(new FieldDecl(module.Intern("Item" + (i + 1)), elements[i], IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset));
        }

        return builder.DeclareStruct(key, key, SourceSpan.None, Origin.FromSource(module.Intern(Language)), fields, StructLayoutKind.C, 0, 0, []);
    }

    public StructId RangeEnumeratorStruct()
    {
        Module module = builder.Module;
        const string key = "range.enumerator";
        if (module.StructsByKey.TryGetValue(key, out StructId existing))
        {
            return existing;
        }

        TypeId i32 = builder.Primitive(TypeKind.I32);
        var fields = new List<FieldDecl>
        {
            new(module.Intern("Current"), i32, IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
            new(module.Intern("End"), i32, IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
        };
        return builder.DeclareStruct(key, key, SourceSpan.None, Origin.FromSource(module.Intern(Language)), fields, StructLayoutKind.C, 0, 0, []);
    }

    public static bool IsString(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_String;

    public TypeId TextType() => builder.SliceOf(builder.Primitive(TypeKind.U8), true, true);

    public StructId RuneStruct()
    {
        Module module = builder.Module;
        const string key = "System.Text.Rune";
        if (module.StructsByKey.TryGetValue(key, out StructId existing))
        {
            return existing;
        }

        var fields = new List<FieldDecl>
        {
            new(module.Intern("Value"), builder.Primitive(TypeKind.I32), IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
        };
        return builder.DeclareStruct(key, key, SourceSpan.None, Origin.FromSource(module.Intern(Language)), fields, StructLayoutKind.C, 0, 0, []);
    }

    public StructId StringEnumeratorStruct()
    {
        Module module = builder.Module;
        const string key = "string.enumerator";
        if (module.StructsByKey.TryGetValue(key, out StructId existing))
        {
            return existing;
        }

        TypeId i32 = builder.Primitive(TypeKind.I32);
        var fields = new List<FieldDecl>
        {
            new(module.Intern("Items"), TextType(), IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
            new(module.Intern("Offset"), i32, IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
            new(module.Intern("Scalar"), i32, IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
        };
        return builder.DeclareStruct(key, key, SourceSpan.None, Origin.FromSource(module.Intern(Language)), fields, StructLayoutKind.C, 0, 0, []);
    }

    public StructId EnumeratorOf(INamedTypeSymbol span, Location location) => EnumeratorOf(Map(span, location));

    public StructId EnumeratorOf(TypeId sliceType)
    {
        Module module = builder.Module;
        string key = "enumerator<" + IrPrinter.TypeText(module, sliceType) + ">";
        if (module.StructsByKey.TryGetValue(key, out StructId existing))
        {
            return existing;
        }

        var fields = new List<FieldDecl>
        {
            new(module.Intern("Items"), sliceType, IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
            new(module.Intern("Index"), builder.Primitive(TypeKind.I32), IdRange.Empty, FieldDecl.NoBits, FieldDecl.NoOffset, FieldDecl.NoBitOffset),
        };
        return builder.DeclareStruct(key, key, SourceSpan.None, Origin.FromSource(module.Intern(Language)), fields, StructLayoutKind.C, 0, 0, []);
    }

    public EnumId EnumOf(INamedTypeSymbol type, Location location)
    {
        if (_enums.TryGetValue(type, out EnumId existing))
        {
            return existing;
        }

        bool source = type.Locations.Any(l => l.IsInSource);
        bool exhaustive = source && !type.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() is "System.FlagsAttribute" or "CDelta.NonExhaustiveAttribute");
        var members = new List<(string Name, Int128 Value)>();
        foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsConst && field.ConstantValue is not null)
            {
                members.Add((field.Name, ToInt128(field.ConstantValue)));
            }
        }

        TypeId underlying = Map(type.EnumUnderlyingType!, location);
        EnumId id = builder.DeclareEnum(
            type.GetDocumentationCommentId()![2..],
            type.ToDisplayString(_nameFormat),
            spanOf(type.Locations.FirstOrDefault() ?? Location.None),
            OriginOf(type),
            underlying,
            exhaustive,
            members);
        _enums.Add(type, id);
        return id;
    }

    public static Int128 ToInt128(object value) => value switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        ulong v => v,
        char v => v,
        _ => throw new ArgumentException("not an integer constant"),
    };

    public int FieldIndex(INamedTypeSymbol type, IFieldSymbol field)
    {
        if (type.IsTupleType)
        {
            for (int i = 0; i < type.TupleElements.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(type.TupleElements[i], field)
                    || SymbolEqualityComparer.Default.Equals(type.TupleElements[i].CorrespondingTupleField, field.CorrespondingTupleField ?? field))
                {
                    return i;
                }
            }

            throw new InvalidOperationException("a field is not an element of its tuple");
        }

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

    public static bool IsSpan(ITypeSymbol? type) => type?.OriginalDefinition.ToDisplayString() == "System.Span<T>";

    public static bool IsReadOnlySpan(ITypeSymbol? type) => type?.OriginalDefinition.ToDisplayString() == "System.ReadOnlySpan<T>";

    public static bool IsSpanLike(ITypeSymbol? type) => IsSpan(type) || IsReadOnlySpan(type);

    public static bool IsNullable(ITypeSymbol? type) => type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool IsVoidStruct(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Void", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

    public TypeId ErrorSetOf(INamedTypeSymbol enumeration) =>
        builder.ErrorSetOf(enumeration.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name));

    public TypeId Map(ITypeSymbol type, Location location)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return builder.ParamType(parameter.Name);
            case IFunctionPointerTypeSymbol function:
                var parameters = function.Signature.Parameters
                    .Select(parameter => new SigParam(ParameterType(parameter, location), ModeOf(parameter.RefKind)))
                    .ToList();
                return builder.FunctionPointer(builder.Signature(
                    ConventionOf(function.Signature),
                    parameters,
                    ReferenceType(Map(function.Signature.ReturnType, location), function.Signature.RefKind),
                    false));
            case IPointerTypeSymbol pointer:
                TypeId element = pointer.PointedAtType.SpecialType == SpecialType.System_Void
                    ? builder.Primitive(TypeKind.Void)
                    : Map(pointer.PointedAtType, location);
                return builder.Pointer(element, TypeFlags.Nullable | TypeFlags.Many);
            case INamedTypeSymbol { IsTupleType: true } tuple:
                return builder.StructType(TupleOf(tuple, location));
            case INamedTypeSymbol { SpecialType: SpecialType.System_String }:
                return TextType();
            case INamedTypeSymbol { Name: "Enumerator", ContainingType.SpecialType: SpecialType.System_String }:
                return builder.StructType(StringEnumeratorStruct());
            case INamedTypeSymbol { Name: "Rune", ContainingNamespace: { Name: "Text", ContainingNamespace.Name: "System" } }:
                return builder.StructType(RuneStruct());
            case INamedTypeSymbol { Name: "RangeEnumerator", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }:
                return builder.StructType(RangeEnumeratorStruct());
            case INamedTypeSymbol { Name: "Enumerator", ContainingType: { } enumerated } when IsSpanLike(enumerated):
                return builder.StructType(EnumeratorOf(enumerated, location));
            case INamedTypeSymbol spanType when IsSpan(spanType) || IsReadOnlySpan(spanType):
                return builder.SliceOf(Map(spanType.TypeArguments[0], location), IsReadOnlySpan(spanType));
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable:
                return builder.OptionalOf(Map(nullable.TypeArguments[0], location));
            case INamedTypeSymbol { Name: "Result", ContainingNamespace: { Name: "CDelta", ContainingNamespace.IsGlobalNamespace: true } } result:
                TypeId payload = result.TypeArguments.Length == 0 || IsVoidStruct(result.TypeArguments[0])
                    ? builder.Primitive(TypeKind.Void)
                    : Map(result.TypeArguments[0], location);
                TypeId error = result.TypeArguments.Length == 2 ? ErrorSetOf((INamedTypeSymbol)result.TypeArguments[1]) : builder.AnyError();
                return builder.ResultOf(payload, error);
            case INamedTypeSymbol { Name: "Error", ContainingNamespace: { Name: "CDelta", ContainingNamespace.IsGlobalNamespace: true } }:
                return builder.AnyError();
            case INamedTypeSymbol { Name: "CLong", ContainingNamespace.Name: "InteropServices" }:
                return builder.Primitive(TypeKind.CLong);
            case INamedTypeSymbol { Name: "CULong", ContainingNamespace.Name: "InteropServices" }:
                return builder.Primitive(TypeKind.CULong);
            case INamedTypeSymbol { Name: "Void", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }:
                return builder.Primitive(TypeKind.Void);
            case INamedTypeSymbol opaque when IsOpaque(opaque):
                return OpaqueOf(opaque, location);
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct, SpecialType: SpecialType.None } named:
                return builder.StructType(StructOf(named, location), [.. named.TypeArguments.Select(argument => Map(argument, location))]);
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Enum } enumeration:
                return builder.EnumType(EnumOf(enumeration, location));
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Delegate } delegateType:
                return builder.ClosureType(DelegateSignature(delegateType, location));
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } union when IsUnion(union):
                return builder.UnionType(UnionOf(union, location));
            case INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface } contract:
                return builder.DynOf(InterfaceType(contract, location));
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
        else if (IsInlineArray(owner, out int length, out _))
        {
            type = builder.ArrayOf(Map(field.Type, location), length);
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

    public static bool IsInlineArray(ITypeSymbol type, out int length, out ITypeSymbol? element)
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
