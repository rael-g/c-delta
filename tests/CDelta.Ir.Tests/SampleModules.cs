using CDelta.Ir;

namespace CDelta.Ir.Tests;

internal static class SampleModules
{
    public static Module Arithmetic(ModuleStage stage, ArithMode mode)
    {
        var module = new Module("Sample") { Stage = stage };
        var builder = new ModuleBuilder(module);
        SourceId source = builder.AddSource("$ROOT/Program.cs", "Program.cs");
        TypeId i32 = builder.Primitive(TypeKind.I32);
        TypeId boolean = builder.Primitive(TypeKind.Bool);
        var span = new SourceSpan(source, 3, 5);

        FunctionId id = builder.DeclareFunction(
            "Sample.Program.Max(System.Int32,System.Int32)",
            "Sample.Program.Max",
            span,
            Origin.FromSource(module.Intern("csharp")),
            [new ParamDecl(module.Intern("a"), i32, ParamMode.Value), new ParamDecl(module.Intern("b"), i32, ParamMode.Value)],
            false,
            i32,
            CallConv.Default,
            LinkageKind.Internal,
            null,
            []);

        var body = new Body();
        var f = new FunctionBuilder(module, body);
        LocalId a = f.NewLocal("a", i32, LocalKind.Param, span);
        LocalId b = f.NewLocal("b", i32, LocalKind.Param, span);
        LocalId sum = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId greater = f.NewLocal(null, boolean, LocalKind.Temp, span);
        BlockId entry = f.NewBlock();
        BlockId thenBlock = f.NewBlock();
        BlockId elseBlock = f.NewBlock();
        f.SetBlock(entry);
        f.Assign(
            f.LocalPlace(greater),
            f.Binop(BinOp.Gt, ArithMode.Default, f.Copy(f.LocalPlace(a)), f.Copy(f.LocalPlace(b))),
            span);
        f.Branch(f.Copy(f.LocalPlace(greater)), thenBlock, elseBlock, span);
        f.SetBlock(thenBlock);
        f.Assign(f.LocalPlace(sum), f.Binop(BinOp.Add, mode, f.Copy(f.LocalPlace(a)), f.Const(builder.IntConstant(i32, 1))), span);
        f.Return(f.Copy(f.LocalPlace(sum)), span);
        f.SetBlock(elseBlock);
        f.Return(f.Copy(f.LocalPlace(b)), span);
        builder.SetBody(id, body);
        return module;
    }
}
