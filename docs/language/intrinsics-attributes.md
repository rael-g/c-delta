# Intrinsics and attributes

Every intrinsic is an ordinary C# declaration in the corelib, so Roslyn type-checks its uses. Its
meaning is implemented by the compiler. The SDK adds `global using static CDelta.Intrinsics;` so
the functions below are called without a prefix.

Before adding an intrinsic, check that C# or its standard API has no construct with the meaning.
The second table lists the C# names CΔ reuses.

## CΔ intrinsics (`CDelta.Intrinsics`)

| Signature | Meaning | Document |
|---|---|---|
| `void Defer(Action body)` | run `body` at scope exit | [memory.md](memory.md#defer) |
| `void ErrDefer(Action body)` | run `body` at scope exit by error | [errors.md](errors.md#errdefer) |
| `void ErrDefer(Action<Error> body)` | the same, receiving the error | [errors.md](errors.md#errdefer) |
| `T Try<T, E>(Result<T, E> r)` | payload or return the error | [errors.md](errors.md#try) |
| `T Try<T>(Result<T> r)` | payload or return the error | [errors.md](errors.md#try) |
| `void Try(Result r)` | return the error if any | [errors.md](errors.md#try) |
| `T Catch<T, E>(Result<T, E> r, T fallback)` | payload or `fallback` | [errors.md](errors.md#handling) |
| `T Catch<T>(Result<T> r, T fallback)` | payload or `fallback` | [errors.md](errors.md#handling) |
| `Error Fail<E>(E error) where E : struct, Enum` | an error set member as `Error` | [errors.md](errors.md#error-unions) |
| `Exception Panic(ReadOnlySpan<byte> message)` | operand of `throw`: panic | [errors.md](errors.md#panics) |
| `Exception Unreachable()` | operand of `throw`: unreachable | [errors.md](errors.md#panics) |
| `T Comptime<T>(Func<T> expr)` | evaluate at comptime | [comptime.md](comptime.md) |
| `void Comptime(Action block)` | run at comptime | [comptime.md](comptime.md) |
| `void CompileError(ReadOnlySpan<byte> message)` | compile error if reached | [comptime.md](comptime.md#errors-at-comptime) |
| `ReadOnlySpan<byte> Embed(ReadOnlySpan<byte> path)` | a file's bytes as static data, path relative to the source file, inside the package (`CD4008`) | [comptime.md](comptime.md) |
| `void Breakpoint()` | a debugger trap instruction | |

## CΔ intrinsic types (`CDelta`)

| Type | Meaning | Document |
|---|---|---|
| `Result`, `Result<T>`, `Result<T, E>` | error unions | [errors.md](errors.md) |
| `Error` | any error of the program | [errors.md](errors.md) |
| `Void` | the zero-size value | [types.md](types.md) |
| `Dyn<I>` with `static I Of<T>(ref T value) where T : I` | interface value from a reference | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| `Builtin` | comptime constants | [comptime.md](comptime.md#builtin) |
| `Startup` with `static int ArgCount()` and `static byte** ArgValues()` | the process arguments as given to C `main`; for the std, which wraps them in `Std.Process.Args()` | [std.md](../std.md#the-mvp-subset) |
| `TypeInfo`, `FieldInfo`, `EnumMemberInfo`, `AttributeInfo`, `TypeKind` | comptime reflection | [comptime.md](comptime.md) |
| `Os`, `Arch`, `Abi`, `BuildMode`, `Endian` | target description | [comptime.md](comptime.md#builtin) |

## CΔ attributes (`CDelta`)

| Attribute | Target | Meaning | Document |
|---|---|---|---|
| `[Import(string header, ...)]` | `static partial class` | import a C header into the class | [c-interop.md](c-interop.md) |
| `[Export]`, `[Export(string name)]` | static method, struct, enum, static field | export with C ABI and into the header | [c-interop.md](c-interop.md#exporting) |
| `[CName(string name)]` | extern method, struct, field | the C name, when it differs | [c-interop.md](c-interop.md) |
| `[CMacro]` | `static extern` method | a call to a C function-like macro | [c-interop.md](c-interop.md#macros) |
| `[CFlexible(string name, Type element)]` | imported struct | a flexible array member | [c-interop.md](c-interop.md#what-a-header-becomes) |
| `[Unsupported(string reason)]` | imported declaration | could not be translated; using it is `CD6003` | [c-interop.md](c-interop.md#what-a-header-becomes) |
| `[Opaque]` | struct | incomplete type, only behind pointers | [types.md](types.md#opaque-structs) |
| `[Bits(int width)]` | integer field | C bit-field | [types.md](types.md#layout) |
| `[Union]` | interface | closed tagged union | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| `[NonExhaustive]` | enum | any value of the underlying type is valid | [types.md](types.md#enums) |
| `[Comptime]` | parameter | comptime-known argument | [comptime.md](comptime.md) |
| `[ComptimeQuota(int branches)]` | method | raise the branch quota | [comptime.md](comptime.md) |
| `[Safety(bool enabled)]` | method, struct, module | override safety checks | [memory.md](memory.md#build-modes-and-illegal-behavior) |
| `[PanicHandler]` | static method | replace the panic handler | [errors.md](errors.md#the-panic-handler) |
| `[Test]` | static method | a test, compiled only by `cdc test` | [modules-build.md](modules-build.md#tests) |
| `[Section(string name)]` | static field, static method | place in a linker section | [c-interop.md](c-interop.md#exporting) |
| `[Linkage(LinkageKind kind)]` | exported symbol | `Strong`, `Weak` or `Internal` | [c-interop.md](c-interop.md#exporting) |

## Reused C# names

| C# name | Meaning in CΔ |
|---|---|
| `[DllImport(string library)]` on `static extern` | a C function from a library linked statically or dynamically by the build |
| `[StructLayout]`, `[FieldOffset]` | struct layout |
| `[InlineArray(n)]` | fixed array struct |
| `[MethodImpl(MethodImplOptions.AggressiveInlining)]`, `NoInlining` | `inline` hint, `noinline` |
| `[SkipLocalsInit]` | `stackalloc` is not zeroed |
| `[ThreadStatic]` | `_Thread_local` |
| `[Flags]` | non-exhaustive bit set enum |
| `[InterpolatedStringHandler]`, `[InterpolatedStringHandlerArgument]` | formatting handler types |
| `[Obsolete]` | deprecation warning |
| `[Conditional(string symbol)]` | calls removed unless the build symbol is defined |
| `Unsafe.SkipInit`, `Unsafe.As`, `Unsafe.BitCast`, `Unsafe.SizeOf`, `Unsafe.Add`, `Unsafe.AsPointer`, `Unsafe.AsRef`, `Unsafe.IsNullRef`, `Unsafe.NullRef` | reinterpretation and raw references |
| `MemoryMarshal.CreateSpan`, `MemoryMarshal.GetReference`, `MemoryMarshal.Cast` | span construction and reinterpretation |
| `Interlocked.*`, `Volatile.Read`, `Volatile.Write` | atomics |
| `Math.*`, `MathF.*` (the subset in the corelib) | mapped to C `<math.h>` or compiler builtins |
| `CLong`, `CULong`, `NFloat` | C `long`, `unsigned long`, and a pointer-sized float |
| `IDisposable` | the `using` contract |
| `CallConvCdecl`, `CallConvStdcall`, `CallConvFastcall`, `CallConvThiscall`, `CallConvVectorcall` | calling conventions in `delegate* unmanaged[...]` and `[UnmanagedCallersOnly]`, only when not the target's default |
| `[UnmanagedCallersOnly(CallConvs = [...])]` | gives a method a non-default calling convention, so that `&Method` converts to `delegate* unmanaged[...]` |

Corelib members that C# exposes as properties or operators on primitives and spans (`Length`,
indexers, ranges, arithmetic and comparison operators, `MinValue`, `MaxValue`) are intrinsics too.
