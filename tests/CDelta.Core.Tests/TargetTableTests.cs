using CDelta.Ir;

namespace CDelta.Core.Tests;

public sealed class TargetTableTests
{
    [Theory]
    [InlineData("x86_64-linux-gnu", 8, 8)]
    [InlineData("aarch64-linux-gnu", 8, 8)]
    [InlineData("x86_64-windows-gnu", 8, 4)]
    [InlineData("wasm32-wasi-musl", 4, 4)]
    public void DescribesTheTarget(string triple, int pointer, int longSize)
    {
        Assert.True(TargetTable.TryGet(triple, out Target target));

        Assert.Equal(pointer, PrimitiveLayout.Size(TypeKind.USize, target));
        Assert.Equal(longSize, PrimitiveLayout.Size(TypeKind.CLong, target));
    }

    [Fact]
    public void UnknownTargetsAreRejected() => Assert.False(TargetTable.TryGet("riscv64-linux-gnu", out _));

    [Fact]
    public void Int128AlignmentFollowsTheTarget()
    {
        TargetTable.TryGet("x86_64-linux-gnu", out Target native);
        TargetTable.TryGet("x86_64-windows-msvc", out Target emulated);

        Assert.Equal(16, PrimitiveLayout.Alignment(TypeKind.I128, native));
        Assert.Equal(8, PrimitiveLayout.Alignment(TypeKind.I128, emulated));
    }
}
