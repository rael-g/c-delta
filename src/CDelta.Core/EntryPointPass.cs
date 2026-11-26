using CDelta.Ir;

namespace CDelta.Core;

public static class EntryPointPass
{
    private const string MainKey = "synthetic:main";
    private const string MainName = "main";
    private const string ArgumentCountKey = "synthetic:argc";
    private const string ArgumentValuesKey = "synthetic:argv";
    private const string ArgumentStringsKey = "synthetic:args";
    private const int FailureExitCode = 1;

    public static void Run(Module module)
    {
        FunctionId? entry = null;
        for (int i = 0; i < module.Functions.Count; i++)
        {
            if (RootsPass.HasAttribute(module, module.Functions[i], RootsPass.EntryPointAttribute))
            {
                entry = new FunctionId(i);
                break;
            }
        }

        if (entry is null)
        {
            return;
        }

        var builder = new ModuleBuilder(module);
        TypeId cInt = builder.Primitive(TypeKind.CInt);
        TypeId cChar = builder.Primitive(TypeKind.CChar);
        TypeId argvType = builder.Pointer(
            builder.Pointer(cChar, TypeFlags.Nullable | TypeFlags.Many),
            TypeFlags.Nullable | TypeFlags.Many);
        FunctionId main = builder.DeclareFunction(
            MainKey,
            MainName,
            SourceSpan.None,
            Origin.Synthetic,
            [new ParamDecl(module.Intern("argc"), cInt, ParamMode.Value), new ParamDecl(module.Intern("argv"), argvType, ParamMode.Value)],
            false,
            cInt,
            CallConv.Default,
            LinkageKind.Export,
            MainName,
            []);

        var body = new Body();
        var f = new FunctionBuilder(module, body);
        f.NewLocal("argc", cInt, LocalKind.Param, SourceSpan.None);
        f.NewLocal("argv", argvType, LocalKind.Param, SourceSpan.None);
        f.SetBlock(f.NewBlock());

        StoreArguments(module, f);
        StoreArgumentStrings(module, builder, f, argvType);
        TypeId result = module.Functions[entry.Value.Index].Return;
        RvalueId call = f.CallDirect(entry.Value, []);
        OperandId exitCode;
        if (module.Types[result.Index].Kind == TypeKind.Void)
        {
            f.CallStatement(call, SourceSpan.None);
            exitCode = f.Const(builder.IntConstant(cInt, 0));
        }
        else
        {
            LocalId value = f.NewLocal(null, result, LocalKind.Temp, SourceSpan.None);
            f.Assign(f.LocalPlace(value), call, SourceSpan.None);
            LocalId converted = f.NewLocal(null, cInt, LocalKind.Temp, SourceSpan.None);
            if (module.Types[result.Index].Kind == TypeKind.Result)
            {
                EmitResultExit(module, builder, f, value, converted, cInt);
            }
            else
            {
                f.Assign(f.LocalPlace(converted), f.Cast(CastKind.IntInt, ArithMode.Wrap, f.Copy(f.LocalPlace(value)), cInt), SourceSpan.None);
            }

            exitCode = f.Copy(f.LocalPlace(converted));
        }

        f.Return(exitCode, SourceSpan.None);
        builder.SetBody(main, body);
    }

    private static void EmitResultExit(Module module, ModuleBuilder builder, FunctionBuilder f, LocalId value, LocalId exitCode, TypeId cInt)
    {
        TypeId resultType = f.Body.Locals[value.Index].Type;
        TypeId payload = new(module.Types[resultType.Index].A);
        TypeId boolean = builder.Primitive(TypeKind.Bool);
        LocalId succeeded = f.NewLocal(null, boolean, LocalKind.Temp, SourceSpan.None);
        f.Assign(f.LocalPlace(succeeded), f.HasPayload(f.LocalPlace(value)), SourceSpan.None);
        BlockId ok = f.NewBlock();
        BlockId failed = f.NewBlock();
        BlockId done = f.NewBlock();
        f.Branch(f.Copy(f.LocalPlace(succeeded)), ok, failed, SourceSpan.None);
        f.SetBlock(ok);
        if (module.Types[payload.Index].Kind == TypeKind.Void)
        {
            f.Assign(f.LocalPlace(exitCode), f.Use(f.Const(builder.IntConstant(cInt, 0))), SourceSpan.None);
        }
        else
        {
            OperandId payloadValue = f.Copy(f.Project(f.LocalPlace(value), ProjectionKind.Payload, 0));
            f.Assign(f.LocalPlace(exitCode), f.Cast(CastKind.IntInt, ArithMode.Wrap, payloadValue, cInt), SourceSpan.None);
        }

        f.Goto(done, SourceSpan.None);
        f.SetBlock(failed);
        EmitReport(module, builder, f, value, resultType);
        f.Assign(f.LocalPlace(exitCode), f.Use(f.Const(builder.IntConstant(cInt, FailureExitCode))), SourceSpan.None);
        f.Goto(done, SourceSpan.None);
        f.SetBlock(done);
    }

    private static void EmitReport(Module module, ModuleBuilder builder, FunctionBuilder f, LocalId value, TypeId resultType)
    {
        TypeId u16 = builder.Primitive(TypeKind.U16);
        TypeId text = builder.SliceOf(builder.Primitive(TypeKind.U8), true);
        LocalId error = f.NewLocal(null, new TypeId(module.Types[resultType.Index].B), LocalKind.Temp, SourceSpan.None);
        f.Assign(f.LocalPlace(error), f.Err(f.LocalPlace(value)), SourceSpan.None);
        LocalId code = f.NewLocal(null, u16, LocalKind.Temp, SourceSpan.None);
        f.Assign(f.LocalPlace(code), f.Errcode(f.Copy(f.LocalPlace(error))), SourceSpan.None);
        LocalId name = f.NewLocal(null, text, LocalKind.Temp, SourceSpan.None);
        PlaceId entry = f.Project(f.GlobalPlace(module.GlobalsByKey[ErrorCodesPass.NameTableKey]), ProjectionKind.Index, f.Copy(f.LocalPlace(code)).Index);
        f.Assign(f.LocalPlace(name), f.Use(f.Copy(entry)), SourceSpan.None);
        f.IntrinsicStatement(f.Intrinsic(IrTyper.ErrorReportIntrinsic, [f.Copy(f.LocalPlace(name))]), SourceSpan.None);
    }

    private static void StoreArgumentStrings(Module module, ModuleBuilder builder, FunctionBuilder f, TypeId argvType)
    {
        if (!module.GlobalsByKey.TryGetValue(ArgumentStringsKey, out GlobalId strings))
        {
            return;
        }

        TypeId i32 = builder.Primitive(TypeKind.I32);
        TypeId usize = builder.Primitive(TypeKind.USize);
        TypeId isize = builder.Primitive(TypeKind.ISize);
        TypeId u8 = builder.Primitive(TypeKind.U8);
        TypeId boolean = builder.Primitive(TypeKind.Bool);
        TypeId text = builder.SliceOf(u8, true, true);
        TypeId textItems = builder.Pointer(text, TypeFlags.Nullable | TypeFlags.Many);
        TypeId bytesPointer = builder.Pointer(u8, TypeFlags.Nullable | TypeFlags.Many | TypeFlags.Const);
        TypeId argPointerType = new(module.Types[argvType.Index].A);
        SourceSpan none = SourceSpan.None;

        LocalId Temp(TypeId type) => f.NewLocal(null, type, LocalKind.Temp, none);
        OperandId Number(TypeId type, int value) => f.Const(builder.IntConstant(type, value));
        OperandId Compute(TypeId type, RvalueId rvalue)
        {
            LocalId local = Temp(type);
            f.Assign(f.LocalPlace(local), rvalue, none);
            return f.Copy(f.LocalPlace(local));
        }

        OperandId count = Compute(i32, f.Cast(CastKind.IntInt, ArithMode.Wrap, f.Copy(f.LocalPlace(new LocalId(0))), i32));
        OperandId size = Compute(usize, f.Cast(CastKind.IntInt, ArithMode.Wrap, count, usize));
        LocalId items = Temp(textItems);
        f.Assign(f.LocalPlace(items), f.Alloca(text, size, AllocaInit.Zero), none);
        LocalId index = Temp(i32);
        f.Assign(f.LocalPlace(index), f.Use(Number(i32, 0)), none);
        LocalId length = Temp(i32);
        BlockId head = f.NewBlock();
        BlockId body = f.NewBlock();
        BlockId scan = f.NewBlock();
        BlockId advance = f.NewBlock();
        BlockId store = f.NewBlock();
        BlockId done = f.NewBlock();
        f.Goto(head, none);

        f.SetBlock(head);
        OperandId more = Compute(boolean, f.Binop(BinOp.Lt, ArithMode.Default, f.Copy(f.LocalPlace(index)), count));
        f.Branch(more, body, done, none);

        f.SetBlock(body);
        OperandId position = Compute(isize, f.Cast(CastKind.IntInt, ArithMode.Wrap, f.Copy(f.LocalPlace(index)), isize));
        OperandId slot = Compute(argvType, f.Binop(BinOp.Add, ArithMode.Wrap, f.Copy(f.LocalPlace(new LocalId(1))), position));
        LocalId slotLocal = Temp(argvType);
        f.Assign(f.LocalPlace(slotLocal), f.Use(slot), none);
        OperandId argument = Compute(argPointerType, f.Use(f.Copy(f.DerefPlace(slotLocal))));
        OperandId raw = Compute(bytesPointer, f.Cast(CastKind.PtrPtr, ArithMode.Default, argument, bytesPointer));
        f.Assign(f.LocalPlace(length), f.Use(Number(i32, 0)), none);
        f.Goto(scan, none);

        f.SetBlock(scan);
        OperandId offset = Compute(isize, f.Cast(CastKind.IntInt, ArithMode.Wrap, f.Copy(f.LocalPlace(length)), isize));
        OperandId at = Compute(bytesPointer, f.Binop(BinOp.Add, ArithMode.Wrap, raw, offset));
        LocalId atLocal = Temp(bytesPointer);
        f.Assign(f.LocalPlace(atLocal), f.Use(at), none);
        OperandId atEnd = Compute(boolean, f.Binop(BinOp.Eq, ArithMode.Default, f.Copy(f.DerefPlace(atLocal)), Number(u8, 0)));
        f.Branch(atEnd, store, advance, none);

        f.SetBlock(advance);
        f.Assign(f.LocalPlace(length), f.Binop(BinOp.Add, ArithMode.Wrap, f.Copy(f.LocalPlace(length)), Number(i32, 1)), none);
        f.Goto(scan, none);

        f.SetBlock(store);
        OperandId target = Compute(textItems, f.Binop(BinOp.Add, ArithMode.Wrap, f.Copy(f.LocalPlace(items)), position));
        LocalId targetLocal = Temp(textItems);
        f.Assign(f.LocalPlace(targetLocal), f.Use(target), none);
        f.Assign(f.DerefPlace(targetLocal), f.Aggregate(text, [raw, f.Copy(f.LocalPlace(length))]), none);
        f.Assign(f.LocalPlace(index), f.Binop(BinOp.Add, ArithMode.Wrap, f.Copy(f.LocalPlace(index)), Number(i32, 1)), none);
        f.Goto(head, none);

        f.SetBlock(done);
        TypeId all = builder.SliceOf(text, true);
        TypeId constItems = builder.Pointer(text, TypeFlags.Nullable | TypeFlags.Many | TypeFlags.Const);
        OperandId frozen = Compute(constItems, f.Cast(CastKind.PtrPtr, ArithMode.Default, f.Copy(f.LocalPlace(items)), constItems));
        f.Assign(f.GlobalPlace(strings), f.Aggregate(all, [frozen, count]), none);
    }

    private static void StoreArguments(Module module, FunctionBuilder f)
    {
        if (module.GlobalsByKey.TryGetValue(ArgumentCountKey, out GlobalId count))
        {
            f.Assign(f.GlobalPlace(count), f.Use(f.Copy(f.LocalPlace(new LocalId(0)))), SourceSpan.None);
        }

        if (module.GlobalsByKey.TryGetValue(ArgumentValuesKey, out GlobalId values))
        {
            f.Assign(f.GlobalPlace(values), f.Use(f.Copy(f.LocalPlace(new LocalId(1)))), SourceSpan.None);
        }
    }
}
