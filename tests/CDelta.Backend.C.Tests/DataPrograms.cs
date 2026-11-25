using CDelta.Ir;

namespace CDelta.Backend.C.Tests;

internal static class DataPrograms
{
    public static Module Build(int tableIndex, bool nullPointer)
    {
        var module = new Module("Data");
        var builder = new ModuleBuilder(module);
        SourceId source = builder.AddSource("$ROOT/Data.cs", "Data.cs");
        var span = new SourceSpan(source, 7, 3);
        TypeId i32 = builder.Primitive(TypeKind.I32);
        TypeId isize = builder.Primitive(TypeKind.ISize);

        FieldDecl Field(string name) => new(module.Intern(name), i32, IdRange.Empty, 0, FieldDecl.NoOffset, FieldDecl.NoBitOffset);
        StructId vec = builder.DeclareStruct("Data.Vec2", "Data.Vec2", span, Origin.Synthetic, [Field("X"), Field("Y")], StructLayoutKind.C, 0, 0, []);
        TypeId vecType = builder.StructType(vec);
        TypeId table = builder.ArrayOf(i32, 4);

        ConstId tableInit = builder.AggregateConstant(
            table,
            [builder.IntConstant(i32, 1), builder.IntConstant(i32, 2), builder.IntConstant(i32, 3), builder.IntConstant(i32, 4)]);
        GlobalId tableGlobal = builder.DeclareGlobal("Data.Table", "Data.Table", span, Origin.Synthetic, table, true, false, tableInit, LinkageKind.Internal, null);
        GlobalId counter = builder.DeclareGlobal("Data.Counter", "Data.Counter", span, Origin.Synthetic, i32, true, false, builder.IntConstant(i32, 5), LinkageKind.Internal, null);

        var origin = Origin.FromSource(module.Intern("csharp"));
        FunctionId main = builder.DeclareFunction("Data.Main", "Data.Main", span, origin, [], false, i32, CallConv.Default, LinkageKind.Internal, null, ["cdelta.EntryPoint"]);
        var body = new Body();
        var f = new FunctionBuilder(module, body);
        TypeId pointerType = builder.Pointer(vecType, nullPointer ? TypeFlags.Nullable | TypeFlags.Many : TypeFlags.None);
        LocalId v = f.NewLocal("v", vecType, LocalKind.User, span);
        LocalId p = f.NewLocal("p", pointerType, LocalKind.User, span);
        LocalId x = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId sum = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId element = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId length = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId offset = f.NewLocal(null, isize, LocalKind.Temp, span);
        LocalId second = f.NewLocal(null, i32, LocalKind.Temp, span);
        LocalId q = f.NewLocal(null, builder.Pointer(vecType, TypeFlags.None), LocalKind.Temp, span);
        f.SetBlock(f.NewBlock());

        f.Assign(f.LocalPlace(v), f.Aggregate(vecType, [f.Const(builder.IntConstant(i32, 3)), f.Const(builder.IntConstant(i32, 4))]), span);
        if (nullPointer)
        {
            f.Assign(f.LocalPlace(p), f.Use(f.Const(builder.NullConstant(pointerType))), span);
        }
        else
        {
            f.Assign(f.LocalPlace(p), f.Addr(f.LocalPlace(v), true), span);
        }

        PlaceId xThroughPointer = f.Project(f.DerefPlace(p), ProjectionKind.Field, 0);
        f.Assign(f.LocalPlace(x), f.Use(f.Copy(xThroughPointer)), span);
        f.Assign(f.LocalPlace(x), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(x)), f.Const(builder.IntConstant(i32, 1))), span);
        f.Assign(xThroughPointer, f.Use(f.Copy(f.LocalPlace(x))), span);

        OperandId index = f.Const(builder.IntConstant(i32, tableIndex));
        PlaceId tableElement = f.Project(f.GlobalPlace(tableGlobal), ProjectionKind.Index, index.Index);
        f.Assign(f.LocalPlace(element), f.Use(f.Copy(tableElement)), span);
        f.Assign(f.LocalPlace(length), f.Len(f.GlobalPlace(tableGlobal)), span);

        f.Assign(f.LocalPlace(q), f.Addr(f.LocalPlace(v), true), span);
        f.Assign(f.LocalPlace(offset), f.Use(f.Const(builder.IntConstant(isize, 0))), span);
        f.Assign(f.LocalPlace(q), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(q)), f.Copy(f.LocalPlace(offset))), span);
        f.Assign(f.LocalPlace(second), f.Use(f.Copy(f.Project(f.LocalPlace(v), ProjectionKind.Field, 1))), span);

        f.Assign(f.LocalPlace(sum), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(x)), f.Copy(f.LocalPlace(second))), span);
        f.Assign(f.LocalPlace(sum), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(sum)), f.Copy(f.LocalPlace(element))), span);
        f.Assign(f.LocalPlace(sum), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(sum)), f.Copy(f.GlobalPlace(counter))), span);
        f.Assign(f.LocalPlace(sum), f.Binop(BinOp.Add, ArithMode.Default, f.Copy(f.LocalPlace(sum)), f.Copy(f.LocalPlace(length))), span);
        f.Return(f.Copy(f.LocalPlace(sum)), span);
        builder.SetBody(main, body);
        return module;
    }
}
