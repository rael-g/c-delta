using CDelta.Ir;

namespace CDelta.Core;

public static class TargetTable
{
    private static readonly Target[] _targets =
    [
        Describe("x86_64-linux-gnu", 8, 8, TypeKind.I32, true, true, true, BitFieldRules.SysV),
        Describe("x86_64-linux-musl", 8, 8, TypeKind.I32, true, true, true, BitFieldRules.SysV),
        Describe("x86_64-freestanding-none", 8, 8, TypeKind.I32, true, true, true, BitFieldRules.SysV),
        Describe("aarch64-linux-gnu", 8, 8, TypeKind.U32, false, true, true, BitFieldRules.SysV),
        Describe("aarch64-freestanding-none", 8, 8, TypeKind.U32, false, true, true, BitFieldRules.SysV),
        Describe("aarch64-macos-none", 8, 8, TypeKind.I32, true, true, true, BitFieldRules.SysV),
        Describe("x86_64-windows-gnu", 8, 4, TypeKind.U16, true, true, true, BitFieldRules.Microsoft),
        Describe("x86_64-windows-msvc", 8, 4, TypeKind.U16, true, false, false, BitFieldRules.Microsoft),
        Describe("wasm32-wasi-musl", 4, 4, TypeKind.I32, true, false, false, BitFieldRules.SysV),
    ];

    public static bool TryGet(string triple, out Target target)
    {
        foreach (Target candidate in _targets)
        {
            if (candidate.Triple == triple)
            {
                target = candidate;
                return true;
            }
        }

        target = default;
        return false;
    }

    private static Target Describe(
        string triple,
        int pointerSize,
        int longSize,
        TypeKind wcharType,
        bool charIsSigned,
        bool nativeInt128,
        bool nativeF16,
        BitFieldRules bitFields) =>
        new(triple, pointerSize, longSize, wcharType, charIsSigned, nativeInt128, nativeF16, bitFields);
}
