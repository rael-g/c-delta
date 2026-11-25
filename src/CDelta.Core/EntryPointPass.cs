using CDelta.Ir;

namespace CDelta.Core;

public static class EntryPointPass
{
    private const string MainKey = "synthetic:main";
    private const string MainName = "main";
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
}
