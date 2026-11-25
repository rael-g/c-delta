using CDelta.Ir;

namespace CDelta.Core;

public static class EntryPointPass
{
    private const string MainKey = "synthetic:main";
    private const string MainName = "main";

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
            f.Assign(f.LocalPlace(converted), f.Cast(CastKind.IntInt, ArithMode.Wrap, f.Copy(f.LocalPlace(value)), cInt), SourceSpan.None);
            exitCode = f.Copy(f.LocalPlace(converted));
        }

        f.Return(exitCode, SourceSpan.None);
        builder.SetBody(main, body);
    }
}
