namespace CDelta.Ir;

internal sealed class BodyParser(
    Module module,
    TypeParser types,
    string[] lines,
    int start,
    int end)
{
    private readonly Body _body = new();
    private FunctionBuilder _f = null!;

    public Body Run()
    {
        _f = new FunctionBuilder(module, _body);
        int blockCount = 0;
        for (int i = start; i < end; i++)
        {
            if (IsBlockLabel(lines[i]))
            {
                blockCount++;
            }
        }

        for (int i = start; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith("local ", StringComparison.Ordinal))
            {
                ParseLocal(lines[i], i);
            }
        }

        for (int i = 0; i < blockCount; i++)
        {
            _f.NewBlock();
        }

        int block = -1;
        for (int i = start; i < end; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("local ", StringComparison.Ordinal) || line.Trim().Length == 0)
            {
                continue;
            }

            if (IsBlockLabel(line))
            {
                block++;
                _f.SetBlock(new BlockId(block));
                continue;
            }

            ParseInstruction(new IrCursor(line, i + 1));
        }

        return _body;
    }

    private static bool IsBlockLabel(string line) =>
        line.StartsWith("bb", StringComparison.Ordinal) && line.TrimEnd().EndsWith(':');

    private void ParseLocal(string line, int index)
    {
        var cursor = new IrCursor(line, index + 1);
        cursor.ExpectWord("local");
        cursor.Expect('%');
        cursor.ReadInt();
        string? name = null;
        if (cursor.Peek != ':')
        {
            name = cursor.ReadWord();
        }

        cursor.Expect(':');
        TypeId type = types.ParseType(cursor);
        string kind = cursor.ReadWord();
        SourceSpan span = cursor.Peek == '@' ? SpanParser.Parse(cursor) : SourceSpan.None;
        _f.NewLocal(name, type, kind switch
        {
            "param" => LocalKind.Param,
            "user" => LocalKind.User,
            _ => LocalKind.Temp,
        }, span);
    }

    private void ParseInstruction(IrCursor cursor)
    {
        string word = cursor.PeekWord();
        char first = cursor.Peek;
        if (first == '%' || first == '(' || word == "global")
        {
            PlaceId place = ParsePlace(cursor);
            cursor.Expect('=');
            RvalueId value = ParseRvalue(cursor);
            _f.Assign(place, value, SpanParser.Parse(cursor));
            return;
        }

        switch (word)
        {
            case "call":
                cursor.ExpectWord("call");
                RvalueId call = ParseCall(cursor);
                _f.CallStatement(call, SpanParser.Parse(cursor));
                return;
            case "intrinsic":
                _f.IntrinsicStatement(ParseRvalue(cursor), SpanParser.Parse(cursor));
                return;
            case "check":
                cursor.ExpectWord("check");
                CheckKind kind = IrNames.ParseCheck(cursor.ReadWord()) ?? throw cursor.Error("unknown check");
                OperandId condition = ParseOperand(cursor);
                string message = cursor.ReadString();
                _f.Check(kind, condition, message, SpanParser.Parse(cursor));
                return;
            case "trace.push":
                cursor.ReadWord();
                _f.TracePush(SpanParser.Parse(cursor));
                return;
            case "trace.save":
            case "trace.restore":
                cursor.ReadWord();
                cursor.Expect('%');
                var traced = new LocalId(cursor.ReadInt());
                if (word == "trace.save")
                {
                    _f.TraceSave(traced, SpanParser.Parse(cursor));
                }
                else
                {
                    _f.TraceRestore(traced, SpanParser.Parse(cursor));
                }

                return;
            case "live":
            case "dead":
                cursor.ReadWord();
                cursor.Expect('%');
                var local = new LocalId(cursor.ReadInt());
                SourceSpan span = SpanParser.Parse(cursor);
                if (word == "live")
                {
                    _f.Live(local, span);
                }
                else
                {
                    _f.Dead(local, span);
                }

                return;
            default:
                ParseTerminator(cursor, word);
                return;
        }
    }

    private void ParseTerminator(IrCursor cursor, string word)
    {
        cursor.ReadWord();
        switch (word)
        {
            case "goto":
                BlockId target = ParseBlock(cursor);
                _f.Goto(target, SpanParser.Parse(cursor));
                return;
            case "branch":
                OperandId condition = ParseOperand(cursor);
                cursor.Expect('?');
                BlockId then = ParseBlock(cursor);
                cursor.Expect(':');
                BlockId otherwise = ParseBlock(cursor);
                _f.Branch(condition, then, otherwise, SpanParser.Parse(cursor));
                return;
            case "switch":
                OperandId value = ParseOperand(cursor);
                cursor.Expect('[');
                var cases = new List<SwitchCase>();
                while (!cursor.TryChar(']'))
                {
                    ConstId constant = types.ParseConstant(cursor);
                    cursor.ExpectText("->");
                    cases.Add(new SwitchCase(constant, ParseBlock(cursor)));
                }

                cursor.ExpectWord("default");
                BlockId fallback = ParseBlock(cursor);
                _f.Switch(value, cases, fallback, SpanParser.Parse(cursor));
                return;
            case "return":
                OperandId result = cursor.Peek == '@' ? OperandId.None : ParseOperand(cursor);
                _f.Return(result, SpanParser.Parse(cursor));
                return;
            case "unreachable":
                string mode = cursor.ReadWord();
                _f.Unreachable(mode switch
                {
                    "trap" => UnreachableMode.Trap,
                    "assume" => UnreachableMode.Assume,
                    _ => UnreachableMode.Default,
                }, SpanParser.Parse(cursor));
                return;
            case "panic":
                OperandId message = ParseOperand(cursor);
                _f.Panic(message, SpanParser.Parse(cursor));
                return;
            default:
                throw cursor.Error("unknown instruction " + word);
        }
    }

    private BlockId ParseBlock(IrCursor cursor)
    {
        cursor.ExpectText("bb");
        return new BlockId(cursor.ReadInt());
    }

    private PlaceId ParsePlace(IrCursor cursor)
    {
        PlaceId place;
        if (cursor.TryWord("global"))
        {
            string key = cursor.ReadReference();
            place = _f.GlobalPlace(module.GlobalsByKey[key]);
        }
        else if (cursor.TryText("(*%"))
        {
            place = _f.DerefPlace(new LocalId(cursor.ReadInt()));
            cursor.Expect(')');
        }
        else
        {
            cursor.Expect('%');
            place = _f.LocalPlace(new LocalId(cursor.ReadInt()));
        }

        while (true)
        {
            if (cursor.TryText(".field("))
            {
                place = _f.Project(place, ProjectionKind.Field, cursor.ReadInt());
                cursor.Expect(')');
            }
            else if (cursor.TryText(".payload("))
            {
                place = _f.Project(place, ProjectionKind.Payload, cursor.ReadInt());
                cursor.Expect(')');
            }
            else if (cursor.TryChar('['))
            {
                OperandId index = ParseOperand(cursor);
                cursor.Expect(']');
                place = _f.Project(place, ProjectionKind.Index, index.Index);
            }
            else
            {
                return place;
            }
        }
    }

    private OperandId ParseOperand(IrCursor cursor)
    {
        string word = cursor.ReadWord();
        switch (word)
        {
            case "copy":
                return _f.Copy(ParsePlace(cursor));
            case "const":
                return _f.Const(types.ParseConstant(cursor));
            case "undef":
                return _f.Undef(types.ParseType(cursor));
            default:
                throw cursor.Error("unknown operand " + word);
        }
    }

    private RvalueId ParseRvalue(IrCursor cursor)
    {
        string word = cursor.PeekWord();
        switch (word)
        {
            case "binop":
                cursor.ReadWord();
                BinOp op = IrNames.ParseBinOp(cursor.ReadWord()) ?? throw cursor.Error("unknown binop");
                ArithMode mode = IrNames.HasMode(op) ? IrNames.ParseMode(cursor.ReadWord()) : ArithMode.Default;
                OperandId left = ParseOperand(cursor);
                cursor.Expect(',');
                return _f.Binop(op, mode, left, ParseOperand(cursor));
            case "unop":
                cursor.ReadWord();
                UnOp unary = IrNames.ParseUnOp(cursor.ReadWord()) ?? throw cursor.Error("unknown unop");
                ArithMode unaryMode = unary == UnOp.Neg ? IrNames.ParseMode(cursor.ReadWord()) : ArithMode.Default;
                return _f.Unop(unary, unaryMode, ParseOperand(cursor));
            case "cast":
                cursor.ReadWord();
                CastKind cast = IrNames.ParseCast(cursor.ReadWord()) ?? throw cursor.Error("unknown cast");
                ArithMode castMode = IrNames.HasMode(cast) ? IrNames.ParseMode(cursor.ReadWord()) : ArithMode.Default;
                OperandId operand = ParseOperand(cursor);
                cursor.ExpectWord("to");
                return _f.Cast(cast, castMode, operand, types.ParseType(cursor));
            case "addr":
            case "addr.mut":
                cursor.ReadWord();
                return _f.Addr(ParsePlace(cursor), word == "addr.mut");
            case "aggregate":
                cursor.ReadWord();
                TypeId type = types.ParseType(cursor);
                cursor.Expect('(');
                var elements = new List<OperandId>();
                while (!cursor.TryChar(')'))
                {
                    if (elements.Count > 0)
                    {
                        cursor.Expect(',');
                    }

                    elements.Add(ParseOperand(cursor));
                }

                return _f.Aggregate(type, elements);
            case "len":
                cursor.ReadWord();
                return _f.Len(ParsePlace(cursor));
            case "slice":
                cursor.ReadWord();
                PlaceId sliced = ParsePlace(cursor);
                cursor.Expect(',');
                OperandId start = ParseOperand(cursor);
                cursor.Expect(',');
                return _f.Slice(sliced, start, ParseOperand(cursor));
            case "alloca":
                cursor.ReadWord();
                TypeId element = types.ParseType(cursor);
                cursor.Expect(',');
                OperandId count = ParseOperand(cursor);
                cursor.Expect(',');
                AllocaInit init = IrNames.ParseAllocaInit(cursor.ReadWord()) ?? throw cursor.Error("unknown alloca init");
                return _f.Alloca(element, count, init);
            case "haspayload":
                cursor.ReadWord();
                return _f.HasPayload(ParsePlace(cursor));
            case "err":
                cursor.ReadWord();
                return _f.Err(ParsePlace(cursor));
            case "errcode":
                cursor.ReadWord();
                return _f.Errcode(ParseOperand(cursor));
            case "result.ok":
                cursor.ReadWord();
                TypeId okType = types.ParseType(cursor);
                return _f.ResultOk(okType, OperandStartsHere(cursor) ? ParseOperand(cursor) : OperandId.None);
            case "result.err":
                cursor.ReadWord();
                TypeId errType = types.ParseType(cursor);
                return _f.ResultErr(errType, ParseOperand(cursor));
            case "intrinsic":
                cursor.ReadWord();
                string intrinsic = cursor.ReadWord();
                cursor.Expect('(');
                var intrinsicArgs = new List<OperandId>();
                while (!cursor.TryChar(')'))
                {
                    if (intrinsicArgs.Count > 0)
                    {
                        cursor.Expect(',');
                    }

                    intrinsicArgs.Add(ParseOperand(cursor));
                }

                return _f.Intrinsic(intrinsic, intrinsicArgs);
            case "optional.some":
                cursor.ReadWord();
                return _f.OptionalSome(ParseOperand(cursor));
            case "optional.none":
                cursor.ReadWord();
                return _f.OptionalNone(types.ParseType(cursor));
            case "sizeof":
                cursor.ReadWord();
                return _f.SizeOf(types.ParseType(cursor));
            case "alignof":
                cursor.ReadWord();
                return _f.AlignOf(types.ParseType(cursor));
            case "offsetof":
                cursor.ReadWord();
                TypeId owner = types.ParseType(cursor);
                return _f.OffsetOf(owner, cursor.ReadInt());
            case "call":
                cursor.ReadWord();
                return ParseCall(cursor);
            case "union.make":
                cursor.ReadWord();
                TypeId unionType = types.ParseType(cursor);
                int member = cursor.ReadInt();
                return _f.UnionMake(unionType, member, ParseOperand(cursor));
            case "tag":
                cursor.ReadWord();
                return _f.Tag(ParsePlace(cursor));
            case "closure.make":
                cursor.ReadWord();
                string closureKey = cursor.ReadReference();
                if (!module.FunctionsByKey.TryGetValue(closureKey, out FunctionId closureFunction))
                {
                    throw cursor.Error("unknown function " + closureKey);
                }

                return _f.ClosureMake(closureFunction, ParseOperand(cursor));
            case "dyn.make":
                cursor.ReadWord();
                return ParseDynMake(cursor);
            default:
                return _f.Use(ParseOperand(cursor));
        }
    }

    private static bool OperandStartsHere(IrCursor cursor)
    {
        string word = cursor.PeekWord();
        return word is "copy" or "const" or "fnref" or "undef";
    }

    private RvalueId ParseDynMake(IrCursor cursor)
    {
        var builder = new ModuleBuilder(module);
        if (cursor.ReadWord() == "impl")
        {
            string key = cursor.ReadReference();
            int index = module.Impls.FindIndex(candidate => module.Text(candidate.Key) == key);
            if (index < 0)
            {
                throw cursor.Error("unknown impl " + key);
            }

            return _f.DynMakeImpl(index, ParseOperand(cursor), builder.DynOf(module.Impls[index].Interface));
        }

        string param = cursor.ReadWord();
        TypeId contract = types.ParseType(cursor);
        return _f.DynMakeConstrained(builder.ConstrainedRef(param, contract, string.Empty), ParseOperand(cursor), builder.DynOf(contract));
    }

    private RvalueId ParseCall(IrCursor cursor)
    {
        string kind = cursor.ReadWord();
        FunctionId callee = FunctionId.None;
        OperandId indirect = OperandId.None;
        var typeArgs = new List<TypeId>();
        int constrainedRef = -1;
        if (kind == "direct")
        {
            string key = cursor.ReadReference();
            if (!module.FunctionsByKey.TryGetValue(key, out callee))
            {
                throw cursor.Error("unknown function " + key);
            }

            if (cursor.TryChar('<'))
            {
                do
                {
                    typeArgs.Add(types.ParseType(cursor));
                }
                while (cursor.TryChar(','));
                cursor.Expect('>');
            }
        }
        else if (kind is "dyn" or "union" or "closure")
        {
            indirect = ParseOperand(cursor);
            constrainedRef = kind == "closure" ? 0 : cursor.ReadInt();
        }
        else if (kind == "constrained")
        {
            cursor.ExpectWord("param");
            string param = cursor.ReadWord();
            TypeId interfaceType = types.ParseType(cursor);
            string method = cursor.ReadWord();
            constrainedRef = new ModuleBuilder(module).ConstrainedRef(param, interfaceType, method);
            if (cursor.TryChar('<'))
            {
                do
                {
                    typeArgs.Add(types.ParseType(cursor));
                }
                while (cursor.TryChar(','));
                cursor.Expect('>');
            }
        }
        else
        {
            indirect = ParseOperand(cursor);
        }

        cursor.Expect('(');
        var arguments = new List<OperandId>();
        while (!cursor.TryChar(')'))
        {
            if (arguments.Count > 0)
            {
                cursor.Expect(',');
            }

            arguments.Add(ParseOperand(cursor));
        }

        if (kind == "constrained")
        {
            return _f.CallConstrained(constrainedRef, arguments, typeArgs);
        }

        if (kind == "dyn")
        {
            return _f.CallDyn(indirect, constrainedRef, arguments);
        }

        if (kind == "union")
        {
            return _f.CallUnion(indirect, constrainedRef, arguments);
        }

        if (kind == "closure")
        {
            return _f.CallClosure(indirect, arguments);
        }

        return kind == "direct" ? _f.CallDirect(callee, arguments, typeArgs) : _f.CallIndirect(indirect, arguments);
    }
}
