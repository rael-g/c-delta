using ClangSharp.Interop;
using CDelta.Ir;
using IrTypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.C;

internal sealed unsafe class CTypeTranslator(ModuleBuilder builder, int importId, IReadOnlyDictionary<string, string> typedefs)
{
    private readonly Dictionary<string, TypeId> _records = [];
    private readonly Dictionary<string, TypeId> _enums = [];
    private readonly Dictionary<string, int> _anonymous = [];

    public IReadOnlyList<CXCursor> PendingRecords => _pendingRecords;

    private readonly List<CXCursor> _pendingRecords = [];

    public void ClearPending() => _pendingRecords.Clear();

    public TypeId Translate(CXType type)
    {
        CXType canonical = type.CanonicalType;
        switch (canonical.kind)
        {
            case CXTypeKind.CXType_Void:
                return builder.Primitive(IrTypeKind.Void);
            case CXTypeKind.CXType_Bool:
                return builder.Primitive(IrTypeKind.Bool);
            case CXTypeKind.CXType_Char_S or CXTypeKind.CXType_Char_U:
                return builder.Primitive(IrTypeKind.CChar);
            case CXTypeKind.CXType_SChar:
                return builder.Primitive(IrTypeKind.CSChar);
            case CXTypeKind.CXType_UChar:
                return builder.Primitive(IrTypeKind.CUChar);
            case CXTypeKind.CXType_Short:
                return builder.Primitive(IrTypeKind.CShort);
            case CXTypeKind.CXType_UShort:
                return builder.Primitive(IrTypeKind.CUShort);
            case CXTypeKind.CXType_Int:
                return builder.Primitive(IrTypeKind.CInt);
            case CXTypeKind.CXType_UInt:
                return builder.Primitive(IrTypeKind.CUInt);
            case CXTypeKind.CXType_Long:
                return builder.Primitive(IrTypeKind.CLong);
            case CXTypeKind.CXType_ULong:
                return builder.Primitive(IrTypeKind.CULong);
            case CXTypeKind.CXType_LongLong:
                return builder.Primitive(IrTypeKind.CLongLong);
            case CXTypeKind.CXType_ULongLong:
                return builder.Primitive(IrTypeKind.CULongLong);
            case CXTypeKind.CXType_Float:
                return builder.Primitive(IrTypeKind.F32);
            case CXTypeKind.CXType_Double:
                return builder.Primitive(IrTypeKind.F64);
            case CXTypeKind.CXType_Pointer:
                return TranslatePointer(canonical);
            case CXTypeKind.CXType_ConstantArray:
                return builder.ArrayOf(Translate(canonical.ElementType), (int)canonical.NumElements);
            case CXTypeKind.CXType_FunctionProto:
            case CXTypeKind.CXType_FunctionNoProto:
                return builder.FunctionPointer(TranslateSignature(canonical));
            case CXTypeKind.CXType_Record:
                return TranslateRecord(canonical.Declaration);
            case CXTypeKind.CXType_Enum:
                return TranslateEnum(canonical.Declaration);
            case CXTypeKind.CXType_LongDouble:
                throw new UnsupportedTypeException("long double");
            case CXTypeKind.CXType_Complex:
                throw new UnsupportedTypeException("_Complex");
            case CXTypeKind.CXType_Atomic:
                throw new UnsupportedTypeException("_Atomic");
            case CXTypeKind.CXType_Vector:
                throw new UnsupportedTypeException("vector type");
            case CXTypeKind.CXType_Int128 or CXTypeKind.CXType_UInt128:
                throw new UnsupportedTypeException("128-bit integer");
            case CXTypeKind.CXType_IncompleteArray:
                return builder.Pointer(Translate(canonical.ElementType), TypeFlags.Nullable | TypeFlags.Many);
            default:
                throw new UnsupportedTypeException("type " + canonical.kind);
        }
    }

    public SigId TranslateSignature(CXType function)
    {
        var parameters = new List<SigParam>();
        for (uint i = 0; i < (uint)function.NumArgTypes; i++)
        {
            parameters.Add(new SigParam(Translate(function.GetArgType(i)), ParamMode.Value));
        }

        return builder.Signature(CallConv.Default, parameters, Translate(function.ResultType), function.IsFunctionTypeVariadic);
    }

    private TypeId TranslatePointer(CXType pointer)
    {
        CXType pointee = pointer.PointeeType;
        TypeFlags flags = TypeFlags.Nullable | TypeFlags.Many;
        if (pointee.IsConstQualified)
        {
            flags |= TypeFlags.Const;
        }

        if (pointee.IsVolatileQualified)
        {
            flags |= TypeFlags.Volatile;
        }

        CXType canonical = pointee.CanonicalType;
        TypeId element = canonical.kind is CXTypeKind.CXType_FunctionProto or CXTypeKind.CXType_FunctionNoProto
            ? builder.FunctionPointer(TranslateSignature(canonical))
            : Translate(pointee);
        if (canonical.kind is CXTypeKind.CXType_FunctionProto or CXTypeKind.CXType_FunctionNoProto)
        {
            return element;
        }

        return builder.Pointer(element, flags);
    }

    public string RecordKey(CXCursor declaration)
    {
        string name = NameOf(declaration);
        return name;
    }

    private string NameOf(CXCursor declaration)
    {
        string typedefName = TypedefNameOf(declaration);
        if (typedefName.Length > 0)
        {
            return typedefName;
        }

        string spelling = declaration.Spelling.ToString();
        if (spelling.Length > 0 && !declaration.IsAnonymous && !spelling.Contains("(unnamed", StringComparison.Ordinal)
            && !spelling.Contains("(anonymous", StringComparison.Ordinal))
        {
            return spelling;
        }

        string usr = declaration.Usr.ToString();
        _anonymous.TryAdd(usr, _anonymous.Count);
        return "anonymous_" + _anonymous[usr];
    }

    private string TypedefNameOf(CXCursor declaration) =>
        typedefs.TryGetValue(declaration.Usr.ToString(), out string? name) ? name : string.Empty;

    private TypeId TranslateRecord(CXCursor declaration)
    {
        CXCursor definition = declaration.Definition;
        bool complete = !definition.IsNull;
        CXCursor anchor = complete ? definition : declaration;
        string usr = anchor.Usr.ToString();
        if (_records.TryGetValue(usr, out TypeId existing))
        {
            return existing;
        }

        string key = RecordKey(anchor);
        string cName = CSpelling(anchor, key);
        Origin origin = new(OriginKind.CHeader, StringId.None, importId, StringId.None, builder.Module.Intern(cName));
        TypeId result;
        if (!complete)
        {
            result = builder.OpaqueType(builder.DeclareOpaque(Unique(builder.Module.OpaquesByKey, key), key, SourceSpan.None, origin));
            _records[usr] = result;
            return result;
        }

        StructId id = builder.BeginStruct(
            Unique(builder.Module.StructsByKey, key),
            key,
            SourceSpan.None,
            origin,
            anchor.kind == CXCursorKind.CXCursor_UnionDecl ? StructLayoutKind.Explicit : IsPacked(anchor) ? StructLayoutKind.Packed : StructLayoutKind.C,
            IsPacked(anchor) ? 1 : 0,
            0,
            []);
        result = builder.StructType(id);
        _records[usr] = result;
        _pendingRecords.Add(anchor);
        return result;
    }

    public void DefineRecord(CXCursor declaration)
    {
        string usr = declaration.Usr.ToString();
        TypeId type = _records[usr];
        StructId id = new(builder.Module.Types[type.Index].A);
        bool isUnion = declaration.kind == CXCursorKind.CXCursor_UnionDecl;
        var fields = new List<FieldDecl>();
        int anonymousMembers = 0;
        string parent = builder.Module.Text(builder.Module.Structs[id.Index].Key);
        CXCursorVisitor visitor = (field, _, _) =>
        {
            if (field.kind != CXCursorKind.CXCursor_FieldDecl)
            {
                return CXChildVisitResult.CXChildVisit_Continue;
            }

            CXType fieldType = field.Type;
            if (fieldType.CanonicalType.kind == CXTypeKind.CXType_IncompleteArray)
            {
                return CXChildVisitResult.CXChildVisit_Continue;
            }

            string name = field.Spelling.ToString();
            if (name.Length == 0)
            {
                name = parent + "_" + anonymousMembers;
                anonymousMembers++;
            }

            TypeId translated = Translate(fieldType);
            int offsetBits = (int)field.OffsetOfField;
            int width = field.IsBitField ? field.FieldDeclBitWidth : FieldDecl.NoBits;
            int offset = isUnion ? 0 : width == FieldDecl.NoBits ? offsetBits / 8 : FieldDecl.NoOffset;
            fields.Add(new FieldDecl(
                builder.Module.Intern(name),
                translated,
                IdRange.Empty,
                width,
                offset,
                width == FieldDecl.NoBits ? FieldDecl.NoBitOffset : offsetBits));
            return CXChildVisitResult.CXChildVisit_Continue;
        };
        declaration.VisitChildren(visitor, default);
        builder.DefineFields(id, fields);
        StructDecl current = builder.Module.Structs[id.Index];
        builder.Module.Structs[id.Index] = current with
        {
            Size = (int)declaration.Type.SizeOf,
            Alignment = (int)declaration.Type.AlignOf,
        };
    }

    private TypeId TranslateEnum(CXCursor declaration)
    {
        string usr = declaration.Usr.ToString();
        if (_enums.TryGetValue(usr, out TypeId existing))
        {
            return existing;
        }

        string key = NameOf(declaration);
        string cName = CSpelling(declaration, key);
        Origin origin = new(OriginKind.CHeader, StringId.None, importId, StringId.None, builder.Module.Intern(cName));
        TypeId underlying = Translate(declaration.EnumDecl_IntegerType);
        var members = new List<(string Name, Int128 Value)>();
        bool unsigned = IsUnsigned(declaration.EnumDecl_IntegerType.CanonicalType.kind);
        CXCursorVisitor visitor = (member, _, _) =>
        {
            if (member.kind == CXCursorKind.CXCursor_EnumConstantDecl)
            {
                members.Add((member.Spelling.ToString(), unsigned ? member.EnumConstantDeclUnsignedValue : member.EnumConstantDeclValue));
            }

            return CXChildVisitResult.CXChildVisit_Continue;
        };
        declaration.VisitChildren(visitor, default);
        EnumId id = builder.DeclareEnum(Unique(builder.Module.EnumsByKey, key), key, SourceSpan.None, origin, underlying, false, members);
        TypeId result = builder.EnumType(id);
        _enums[usr] = result;
        return result;
    }

    private static bool IsUnsigned(CXTypeKind kind) =>
        kind is CXTypeKind.CXType_UInt or CXTypeKind.CXType_ULong or CXTypeKind.CXType_ULongLong
            or CXTypeKind.CXType_UShort or CXTypeKind.CXType_UChar or CXTypeKind.CXType_Bool;

    private static string Unique<T>(Dictionary<string, T> table, string key)
    {
        string candidate = key;
        int counter = 1;
        while (table.ContainsKey(candidate))
        {
            candidate = key + "_" + counter++;
        }

        return candidate;
    }

    private static bool IsPacked(CXCursor declaration)
    {
        bool packed = false;
        CXCursorVisitor visitor = (child, _, _) =>
        {
            if (child.kind == CXCursorKind.CXCursor_PackedAttr)
            {
                packed = true;
            }

            return CXChildVisitResult.CXChildVisit_Continue;
        };
        declaration.VisitChildren(visitor, default);
        return packed;
    }

    private string CSpelling(CXCursor declaration, string key)
    {
        string typedefName = TypedefNameOf(declaration);
        if (typedefName.Length > 0)
        {
            return typedefName;
        }

        string tag = declaration.Spelling.ToString();
        if (tag.Length == 0 || tag.Contains("(unnamed", StringComparison.Ordinal) || tag.Contains("(anonymous", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string keyword = declaration.kind switch
        {
            CXCursorKind.CXCursor_UnionDecl => "union ",
            CXCursorKind.CXCursor_EnumDecl => "enum ",
            _ => "struct ",
        };
        return keyword + tag;
    }
}
