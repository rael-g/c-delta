using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Ir.Tests;

public sealed class IrVerifierTests
{
    [Fact]
    public void AcceptsAWellFormedLoweredModule()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);

        Assert.False(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }

    [Fact]
    public void RejectsADefaultModeInTheFinalStage()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Final, ArithMode.Default);

        DiagnosticBag bag = IrVerifier.Verify(module, ModuleStage.Final);

        Assert.Contains(bag.Items, d => d.Code == DiagnosticCode.CD9002);
    }

    [Fact]
    public void AcceptsAResolvedModeInTheFinalStage()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Final, ArithMode.Trap);

        Assert.False(IrVerifier.Verify(module, ModuleStage.Final).HasError);
    }

    [Fact]
    public void RejectsABlockWithoutATerminator()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);
        Body body = module.Bodies[0]!;
        body.Blocks[2] = body.Blocks[2] with { Terminator = null };

        Assert.True(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }

    [Fact]
    public void RejectsAnUnreachableBlock()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);
        Body body = module.Bodies[0]!;
        Block entry = body.Blocks[0];
        body.Blocks[0] = entry with { Terminator = body.Blocks[1].Terminator };

        Assert.True(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }

    [Fact]
    public void RejectsAUseBeforeAssignment()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);
        Body body = module.Bodies[0]!;
        var f = new FunctionBuilder(module, body);
        LocalId unassigned = f.NewLocal(null, module.Type(new TypeNode(TypeKind.I32, TypeFlags.None, 0, 0)), LocalKind.Temp, SourceSpan.None);
        Block block = body.Blocks[2];
        Terminator terminator = block.Terminator!.Value;
        body.Operands.Add(new Operand(OperandKind.Copy, f.LocalPlace(unassigned).Index));
        body.Blocks[2] = block with
        {
            Terminator = terminator with { Operand = new OperandId(body.Operands.Count - 1) },
        };

        Assert.True(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }

    [Fact]
    public void RejectsAnAssignmentOfMismatchedTypes()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);
        Body body = module.Bodies[0]!;
        Statement first = body.Statements[0];
        body.Places[first.Place.Index] = new Place(PlaceBase.Local, 2, IdRange.Empty);

        Assert.True(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }

    [Fact]
    public void RejectsACheckBeforeTheFinalStage()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);
        Body body = module.Bodies[0]!;
        var f = new FunctionBuilder(module, body);
        f.SetBlock(new BlockId(2));
        f.Check(CheckKind.DivZero, f.Copy(f.LocalPlace(new LocalId(3))), "division by zero", new SourceSpan(new SourceId(0), 1, 1));

        Assert.True(IrVerifier.Verify(module, ModuleStage.Lowered).HasError);
    }
}
