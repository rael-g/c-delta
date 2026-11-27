namespace CDelta.Ir;

internal sealed class TypeParser(Module module, ModuleBuilder builder)
{
    private static readonly TypeKind[] _primitives =
    [
        TypeKind.Void, TypeKind.NoReturn, TypeKind.Bool, TypeKind.I8, TypeKind.I16, TypeKind.I32, TypeKind.I64,
        TypeKind.I128, TypeKind.U8, TypeKind.U16, TypeKind.U32, TypeKind.U64, TypeKind.U128, TypeKind.ISize,
        TypeKind.USize, TypeKind.F16, TypeKind.F32, TypeKind.F64, TypeKind.CChar, TypeKind.CSChar, TypeKind.CUChar,
        TypeKind.CShort, TypeKind.CUShort, TypeKind.CInt, TypeKind.CUInt, TypeKind.CLong, TypeKind.CULong,
        TypeKind.CLongLong, TypeKind.CULongLong,
    ];

    public ParamMode ParseMode(IrCursor cursor)
    {
        if (cursor.TryWord("ref.readonly"))
        {
            return ParamMode.RefReadonly;
        }

        if (cursor.TryWord("ref"))
        {
            return ParamMode.Ref;
        }

        return cursor.TryWord("out") ? ParamMode.Out : ParamMode.Value;
    }

    public TypeId ParseType(IrCursor cursor)
    {
        string word = cursor.ReadWord();
        foreach (TypeKind kind in _primitives)
        {
            if (IrTypes.Name(kind) == word)
            {
                return builder.Primitive(kind);
            }
        }

        switch (word)
        {
            case "ptr":
                cursor.Expect('<');
                TypeId element = ParseType(cursor);
                cursor.Expect('>');
                TypeFlags flags = TypeFlags.None;
                while (true)
                {
                    if (cursor.TryWord("nullable"))
                    {
                        flags |= TypeFlags.Nullable;
                    }
                    else if (cursor.TryWord("const"))
                    {
                        flags |= TypeFlags.Const;
                    }
                    else if (cursor.TryWord("volatile"))
                    {
                        flags |= TypeFlags.Volatile;
                    }
                    else if (cursor.TryWord("many"))
                    {
                        flags |= TypeFlags.Many;
                    }
                    else
                    {
                        break;
                    }
                }

                return builder.Pointer(element, flags);
            case "slice":
                cursor.Expect('<');
                TypeId sliced = ParseType(cursor);
                cursor.Expect('>');
                bool sliceConst = cursor.TryWord("const");
                return builder.SliceOf(sliced, sliceConst, cursor.TryWord("text"));
            case "optional":
                cursor.Expect('<');
                TypeId payload = ParseType(cursor);
                cursor.Expect('>');
                return builder.OptionalOf(payload);
            case "result":
                cursor.Expect('<');
                TypeId valueType = ParseType(cursor);
                cursor.Expect(',');
                TypeId errorType = ParseType(cursor);
                cursor.Expect('>');
                return builder.ResultOf(valueType, errorType);
            case "anyerror":
                return builder.AnyError();
            case "errorset":
                cursor.Expect('{');
                var names = new List<string>();
                do
                {
                    names.Add(cursor.ReadWord());
                }
                while (cursor.TryChar(','));
                cursor.Expect('}');
                return builder.ErrorSetOf(names);
            case "array":
                cursor.Expect('<');
                TypeId item = ParseType(cursor);
                cursor.Expect(',');
                int length = cursor.ReadInt();
                cursor.Expect('>');
                return builder.ArrayOf(item, length);
            case "fn":
                return builder.FunctionPointer(ParseSig(cursor));
            case "struct":
                string structKey = cursor.ReadReference();
                if (!module.StructsByKey.TryGetValue(structKey, out StructId structId))
                {
                    throw cursor.Error("unknown struct " + structKey);
                }

                var structArgs = new List<TypeId>();
                if (cursor.TryChar('<'))
                {
                    do
                    {
                        structArgs.Add(ParseType(cursor));
                    }
                    while (cursor.TryChar(','));
                    cursor.Expect('>');
                }

                return builder.StructType(structId, structArgs);
            case "interface":
                string interfaceKey = cursor.ReadReference();
                if (!module.InterfacesByKey.TryGetValue(interfaceKey, out InterfaceId interfaceId))
                {
                    throw cursor.Error("unknown interface " + interfaceKey);
                }

                var interfaceArgs = new List<TypeId>();
                if (cursor.TryChar('<'))
                {
                    do
                    {
                        interfaceArgs.Add(ParseType(cursor));
                    }
                    while (cursor.TryChar(','));
                    cursor.Expect('>');
                }

                return builder.InterfaceType(interfaceId, interfaceArgs);
            case "param":
                return builder.ParamType(cursor.ReadWord());
            case "comptime.type":
                return builder.Primitive(TypeKind.ComptimeType);
            case "opaque":
                string opaqueKey = cursor.ReadReference();
                if (!module.OpaquesByKey.TryGetValue(opaqueKey, out OpaqueId opaqueId))
                {
                    throw cursor.Error("unknown opaque " + opaqueKey);
                }

                return builder.OpaqueType(opaqueId);
            case "enum":
                string enumKey = cursor.ReadReference();
                if (!module.EnumsByKey.TryGetValue(enumKey, out EnumId enumId))
                {
                    throw cursor.Error("unknown enum " + enumKey);
                }

                return builder.EnumType(enumId);
            default:
                throw cursor.Error("unknown type " + word);
        }
    }

    public SigId ParseSig(IrCursor cursor)
    {
        cursor.Expect('(');
        var parameters = new List<SigParam>();
        bool variadic = false;
        while (!cursor.TryChar(')'))
        {
            if (parameters.Count > 0 || variadic)
            {
                cursor.Expect(',');
            }

            if (cursor.TryText("..."))
            {
                variadic = true;
                continue;
            }

            ParamMode mode = ParseMode(cursor);
            parameters.Add(new SigParam(ParseType(cursor), mode));
        }

        cursor.ExpectText("->");
        ParamMode returnMode = ParseMode(cursor);
        TypeId result = ParseType(cursor);
        CallConv conv = cursor.TryWord("cc") ? IrNames.ParseConv(cursor.ReadWord()) : CallConv.Default;
        return builder.Signature(conv, parameters, result, variadic, returnMode);
    }

    public ConstId ParseConstant(IrCursor cursor)
    {
        TypeId type = ParseType(cursor);
        return ParseValue(cursor, type);
    }

    public ConstId ParseValue(IrCursor cursor, TypeId type)
    {
        if (cursor.TryChar('{'))
        {
            var items = new List<ConstId>();
            TypeNode node = module.Types[type.Index];
            int index = 0;
            while (!cursor.TryChar('}'))
            {
                if (index > 0)
                {
                    cursor.Expect(',');
                }

                items.Add(ParseValue(cursor, ElementType(node, index)));
                index++;
            }

            return builder.AggregateConstant(type, items);
        }

        if (cursor.Peek == '-' || char.IsDigit(cursor.Peek))
        {
            return builder.IntConstant(type, cursor.ReadInt128());
        }

        string word = cursor.ReadWord();
        switch (word)
        {
            case "true":
                return builder.BoolConstant(type, true);
            case "false":
                return builder.BoolConstant(type, false);
            case "null":
                return builder.NullConstant(type);
            case "undef":
                return builder.UndefConstant(type);
            case "bits":
                return builder.FloatConstant(type, cursor.ReadHex());
            case "bytes":
                return builder.BytesConstant(type, cursor.ReadStringBytes());
            case "error":
                return builder.ErrorConstant(type, cursor.ReadWord());
            case "type":
                return builder.TypeConstant(ParseType(cursor));
            default:
                return builder.IntConstant(type, Int128.Parse(word));
        }
    }

    private TypeId ElementType(TypeNode node, int index)
    {
        switch (node.Kind)
        {
            case TypeKind.Array:
                return new TypeId(node.A);
            case TypeKind.Struct:
                StructDecl declaration = module.Structs[node.A];
                return module.Fields[declaration.Fields.Start + index].Type;
            default:
                return index == 0
                    ? builder.Pointer(builder.Primitive(TypeKind.Void), TypeFlags.Nullable | TypeFlags.Many)
                    : builder.Primitive(TypeKind.I32);
        }
    }
}
