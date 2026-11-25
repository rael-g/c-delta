using CDelta.Ir;

namespace CDelta.Core.Tests;

public sealed class StructLayoutTests
{
    private static (StructDecl Declaration, int[] Offsets) Layout(
        StructLayoutKind kind,
        int pack,
        int minSize,
        params (TypeKind Type, int Offset)[] fields)
    {
        var module = new Module("Layouts");
        var builder = new ModuleBuilder(module);
        var declared = new List<FieldDecl>();
        for (int i = 0; i < fields.Length; i++)
        {
            declared.Add(new FieldDecl(module.Intern("f" + i), builder.Primitive(fields[i].Type), IdRange.Empty, 0, fields[i].Offset));
        }

        StructId id = builder.DeclareStruct("S", "S", SourceSpan.None, Origin.Synthetic, declared, kind, pack, minSize, []);
        TargetTable.TryGet("x86_64-linux-gnu", out Target target);
        LayoutPass.Run(module, target);
        StructDecl declaration = module.Structs[id.Index];
        int[] offsets = new int[fields.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = module.Fields[declaration.Fields.Start + i].Offset;
        }

        return (declaration, offsets);
    }

    [Fact]
    public void AutoSortsByDecreasingAlignmentKeepingTheOrderOfEquals()
    {
        (StructDecl declaration, int[] offsets) = Layout(
            StructLayoutKind.Auto,
            0,
            0,
            (TypeKind.I8, -1),
            (TypeKind.I64, -1),
            (TypeKind.I8, -1),
            (TypeKind.I32, -1));

        Assert.Equal([12, 0, 13, 8], offsets);
        Assert.Equal(16, declaration.Size);
        Assert.Equal(8, declaration.Alignment);
    }

    [Fact]
    public void ExplicitOffsetsMakeAUnion()
    {
        (StructDecl declaration, int[] offsets) = Layout(
            StructLayoutKind.Explicit,
            0,
            0,
            (TypeKind.I32, 0),
            (TypeKind.F64, 0),
            (TypeKind.I8, 0));

        Assert.Equal([0, 0, 0], offsets);
        Assert.Equal(8, declaration.Size);
        Assert.Equal(8, declaration.Alignment);
    }

    [Fact]
    public void ExplicitSizeIsTheMaximumEndRoundedToTheAlignment()
    {
        (StructDecl declaration, _) = Layout(StructLayoutKind.Explicit, 0, 0, (TypeKind.I32, 0), (TypeKind.I32, 6));

        Assert.Equal(12, declaration.Size);
    }

    [Fact]
    public void MinimumSizeGrowsTheStruct()
    {
        (StructDecl declaration, _) = Layout(StructLayoutKind.C, 0, 20, (TypeKind.I32, -1));

        Assert.Equal(20, declaration.Size);
        Assert.Equal(4, declaration.Alignment);
    }

    [Fact]
    public void MinimumSizeIsRoundedToTheAlignment()
    {
        (StructDecl declaration, _) = Layout(StructLayoutKind.C, 0, 10, (TypeKind.I64, -1));

        Assert.Equal(16, declaration.Size);
    }

    [Fact]
    public void AnEmptyStructHasSizeZeroAndAlignmentOne()
    {
        (StructDecl declaration, _) = Layout(StructLayoutKind.C, 0, 0);

        Assert.Equal(0, declaration.Size);
        Assert.Equal(1, declaration.Alignment);
    }
}
