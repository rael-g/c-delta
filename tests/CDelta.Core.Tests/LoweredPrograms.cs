using CDelta.Ir;

namespace CDelta.Core.Tests;

internal static class LoweredPrograms
{
    public static Module DivideAndMain(SafetyOverride safety = SafetyOverride.Default)
    {
        var module = new Module("Sample");
        var builder = new ModuleBuilder(module);
        SourceId source = builder.AddSource("$ROOT/Program.cs", "Program.cs");
        TypeId i32 = builder.Primitive(TypeKind.I32);
        var span = new SourceSpan(source, 3, 5);
        var origin = Origin.FromSource(module.Intern("csharp"));

        FunctionId divide = builder.DeclareFunction(
            "Sample.Program.Divide(System.Int32,System.Int32)",
            "Sample.Program.Divide",
            span,
            origin,
            [new ParamDecl(module.Intern("a"), i32, ParamMode.Value), new ParamDecl(module.Intern("b"), i32, ParamMode.Value)],
            false,
            i32,
            CallConv.Default,
            LinkageKind.Internal,
            null,
            []);
        module.Functions[divide.Index] = module.Functions[divide.Index] with { Safety = safety };
        var divideBody = new Body();
        var d = new FunctionBuilder(module, divideBody);
        LocalId a = d.NewLocal("a", i32, LocalKind.Param, span);
        LocalId b = d.NewLocal("b", i32, LocalKind.Param, span);
        LocalId q = d.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId sum = d.NewLocal(null, i32, LocalKind.Temp, span);
        d.SetBlock(d.NewBlock());
        d.Assign(d.LocalPlace(q), d.Binop(BinOp.Div, ArithMode.Default, d.Copy(d.LocalPlace(a)), d.Copy(d.LocalPlace(b))), span);
        d.Assign(d.LocalPlace(sum), d.Binop(BinOp.Add, ArithMode.Default, d.Copy(d.LocalPlace(q)), d.Const(builder.IntConstant(i32, 1))), span);
        d.Return(d.Copy(d.LocalPlace(sum)), span);
        builder.SetBody(divide, divideBody);

        FunctionId main = builder.DeclareFunction(
            "Sample.Program.Main",
            "Sample.Program.Main",
            span,
            origin,
            [],
            false,
            i32,
            CallConv.Default,
            LinkageKind.Internal,
            null,
            ["cdelta.EntryPoint"]);
        var mainBody = new Body();
        var m = new FunctionBuilder(module, mainBody);
        LocalId r = m.NewLocal(null, i32, LocalKind.Temp, span);
        m.SetBlock(m.NewBlock());
        m.Assign(
            m.LocalPlace(r),
            m.CallDirect(divide, [m.Const(builder.IntConstant(i32, 7)), m.Const(builder.IntConstant(i32, 2))]),
            span);
        m.Return(m.Copy(m.LocalPlace(r)), span);
        builder.SetBody(main, mainBody);

        FunctionId unused = builder.DeclareFunction(
            "Sample.Program.Unused",
            "Sample.Program.Unused",
            span,
            origin,
            [],
            false,
            builder.Primitive(TypeKind.Void),
            CallConv.Default,
            LinkageKind.Internal,
            null,
            []);
        var unusedBody = new Body();
        var u = new FunctionBuilder(module, unusedBody);
        u.SetBlock(u.NewBlock());
        u.Return(OperandId.None, span);
        builder.SetBody(unused, unusedBody);
        return module;
    }
}
