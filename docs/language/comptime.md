# Comptime

Comptime is CΔ code executed by the compiler. It replaces the preprocessor, static
initialization at run time, and most code generation.

## What runs at comptime

| Form | Meaning |
|---|---|
| `const` | A C# constant. Unchanged. |
| `static readonly` field initializers, and initializers of mutable `static` fields | Always evaluated at comptime. The result is stored as static data. A static initializer that cannot be evaluated is `CD4006`. |
| `Comptime(() => expr)` | Evaluates `expr` at comptime and yields it as a constant. |
| `Comptime(() => { ... })` | Runs a block at comptime, for checks. |
| `[Comptime]` parameter | The argument must be comptime-known (`CD4003`); the function is instantiated once per distinct value, like a generic. |
| Generic type arguments | Comptime type parameters. |
| `Builtin.*`, `TypeInfo.Of<T>()`, `sizeof(T)`, `typeof(T)`, `Embed(path)` | Comptime-known values. |
| A condition made only of comptime-known values | Folded; the branch not taken is not compiled. |

A table computed once by the compiler:

```csharp
[InlineArray(256)] struct Table256 { uint _e; }

static class Crc
{
    public static readonly Table256 Table = MakeTable();

    static Table256 MakeTable()
    {
        var table = new Table256();
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[(int)i] = c;
        }
        return table;
    }
}
```

## `[Comptime]` parameters

```csharp
static uint Rotate([Comptime] int bits, uint value) => (value << bits) | (value >> (32 - bits));
```

Within the function, `bits` is a constant: it can size a `stackalloc`, select a branch, or be
passed to another `[Comptime]` parameter. A `[Comptime]` parameter of a method of a generic struct
is part of the instantiation key together with the type arguments.

## What comptime code can do

Any CΔ code, executed by an interpreter over Delta IR with the target's sizes, alignment and
endianness, so that `sizeof`, layout and overflow behave exactly as at run time. In particular:
loops, recursion, structs, spans, pointers into comptime memory, generics, `Result`, `Try`,
delegates, and allocation through `Builtin.ComptimeAllocator`.

It cannot (`CD4010` unless stated):

- call an `extern` function, imported or `[DllImport]` (`CD4001`);
- use `volatile`, `Interlocked`, `Volatile`, thread-local storage, or threads;
- convert a pointer to an integer and back (pointers at comptime are not addresses);
- read or write files, except through `Embed`.

Illegal behavior during comptime is always a compile error, in every build mode (`CD4007`).

Evaluation is bounded by a branch quota: 1,000,000 backward branches and calls per top-level
evaluation (`CD4002`). `[ComptimeQuota(n)]` on a function raises it for evaluations it starts.

## Results of comptime

A comptime result becomes static data. It may contain pointers into memory allocated with
`Builtin.ComptimeAllocator`; that memory becomes static data too. It may not contain pointers
into a comptime stack frame (`CD4009`), nor comptime-only values (`CD4005`).

## Comptime-only values

`System.Type` (from `typeof`) and `TypeInfo` exist only at comptime. `TypeKind` is `Bool`, `Int`, `Float`, `Pointer`, `Span`,
`Struct`, `Enum`, `Union`, `Optional`, `Result`, `Delegate`, `FunctionPointer`, `Interface` or `Void`. Comparing them, reading them
and branching on them is fine; storing one into runtime state is `CD4005`.

```csharp
public readonly struct TypeInfo
{
    public static TypeInfo Of<T>();
    public readonly TypeKind Kind;
    public readonly ReadOnlySpan<byte> Name;
    public readonly int Size;
    public readonly int Alignment;
    public readonly bool Signed;
    public readonly ReadOnlySpan<FieldInfo> Fields;
    public readonly ReadOnlySpan<EnumMemberInfo> Members;
    public readonly ReadOnlySpan<AttributeInfo> Attributes;
    public TypeInfo Element();
}
```

`FieldInfo` has the name, the offset, the `TypeInfo` of the field and its attributes.
`AttributeInfo` exposes the attribute type and its constructor arguments and named arguments as
comptime values. The full API is in the corelib.

Type checking is C#'s: comptime reflection reads descriptions of types, it does not create
expressions whose type depends on a comptime value. Accessing field `i` of a struct generically is
done by offset and `TypeInfo` (as the std formatter does), not by name.

## Errors at comptime

`CompileError("message")` reports `CD4004` at the call site when it is reached during comptime or
remains in a compiled branch after folding:

```csharp
static int BitsOf<T>() where T : unmanaged
{
    if (Unsafe.SizeOf<T>() > 8) CompileError("BitsOf supports up to 64 bits");
    return Unsafe.SizeOf<T>() * 8;
}
```

`Comptime(() => { if (!cond) CompileError("..."); })` is the static assertion.

## Builtin

`Builtin` is a corelib static class of comptime constants supplied by the compiler:

| Member | Type | Meaning |
|---|---|---|
| `Os` | `Os` | `Linux`, `Windows`, `MacOs`, `FreeBsd`, `Wasi`, `Freestanding` |
| `Arch` | `Arch` | `X86_64`, `Aarch64`, `X86`, `Arm`, `RiscV64`, `Wasm32` |
| `Abi` | `Abi` | `Gnu`, `Musl`, `Msvc`, `None` |
| `Mode` | `BuildMode` | `Debug`, `ReleaseSafe`, `ReleaseFast`, `ReleaseSmall` |
| `Endian` | `Endian` | `Little`, `Big` |
| `PointerSize` | `int` | bytes |
| `LinkLibC` | `bool` | whether libc is linked |
| `IsTest` | `bool` | building tests |
| `SafetyChecks` | `bool` | whether the current function has safety checks |
| `ComptimeAllocator` | `IAllocator` | comptime only |
| `Option<T>(name)` | `T` | a build option from the project file |

## Platform code

Both branches of `if (Builtin.Os == Os.Windows)` must type-check under Roslyn, because C# checks
all code. When one branch uses declarations that exist only on one platform (a header imported
only on Windows), use `#if` on the build symbols ([modules-build.md](modules-build.md)) or a file
selected by the build.

## What comptime does not do

It does not produce new types or new declarations, because Roslyn must see every declaration
before comptime runs. Generics produce types; declarations that must be generated come from a
Roslyn source generator ([modules-build.md](modules-build.md#generated-sources)), which can leave
the computing to comptime.
