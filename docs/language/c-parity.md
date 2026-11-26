# Parity with C

Anything a C program can do, a CΔ program can do, at worst through an explicit form (D157). This
document lists every capability of C and its form in CΔ. A capability C has and this table lacks
is a design gap, not a limit of the language.

| C | CΔ | Where |
|---|---|---|
| functions, globals, `static` linkage | static methods and fields of a `static class`; visibility | [modules-build.md](modules-build.md#visibility) |
| `struct`, `union`, `enum`, `typedef` | structs, explicit layout, enums, type names | [types.md](types.md) |
| bit-fields | `[Bits(n)]` | [types.md](types.md#layout) |
| `packed`, `aligned` | `[StructLayout(Pack = n)]`, `[Align(n)]` | [types.md](types.md#layout), [intrinsics-attributes.md](intrinsics-attributes.md) |
| pointers, pointer arithmetic, `void*`, function pointers | `T*`, `void*`, `delegate*<...>` | [types.md](types.md#references-pointers-and-slices) |
| `volatile` | `volatile` fields, `Volatile.Read` and `Volatile.Write` | [intrinsics-attributes.md](intrinsics-attributes.md) |
| `restrict` | kept on imported pointers; `[NoAlias]` on parameters | [intrinsics-attributes.md](intrinsics-attributes.md) |
| `_Atomic`, `<stdatomic.h>` | `Interlocked`, `Atomic<T>` | [memory.md](memory.md#threads-and-atomics) |
| `_Thread_local` | `[ThreadStatic]` | [intrinsics-attributes.md](intrinsics-attributes.md) |
| `alloca`, VLAs | `stackalloc` | [types.md](types.md#arrays) |
| `goto` | `goto` | [subset.md](subset.md#statements) |
| variadic calls | `__arglist` | [c-interop.md](c-interop.md) |
| defining a variadic function | not supported; take a span instead | |
| `setjmp`, `longjmp` | callable through an import; a `longjmp` across CΔ frames skips their `Defer` bodies, as it skips C cleanup | [c-interop.md](c-interop.md) |
| the preprocessor | comptime, `#if` on build symbols, `[CMacro]` | [comptime.md](comptime.md) |
| object-like macros of constants | `const` | [c-interop.md](c-interop.md#what-a-header-becomes) |
| object-like macros of a constant address (`#define GPIOA ((GPIO_TypeDef*)GPIOA_BASE)`) | `[CMacro] static readonly` pointer fields; a read emits the macro | [c-interop.md](c-interop.md#what-a-header-becomes) |
| function-like macros | `[CMacro]` methods | [c-interop.md](c-interop.md#macros) |
| `__attribute__((section))`, `weak` | `[Section]`, `[Linkage]` | [intrinsics-attributes.md](intrinsics-attributes.md) |
| `__attribute__((naked))` | `[Naked]` | [intrinsics-attributes.md](intrinsics-attributes.md) |
| interrupt handlers | `[Interrupt(kind)]`, the target's interrupt attribute | [intrinsics-attributes.md](intrinsics-attributes.md) |
| inline assembly | `Asm(...)`, GNU extended assembly; assembly sources as `AsmSource` items | this document |
| SIMD types and intrinsics | `Vector64<T>`, `Vector128<T>`, `Vector256<T>`, `Vector512<T>` as corelib types with operators, over the C compilers' vector extensions; the intrinsic headers import against them | this document |
| freestanding code, no libc | `LinkLibC=false`, `*-freestanding-none` and the embedded targets | [modules-build.md](modules-build.md#targets) |
| a custom entry symbol, a reset vector | `EntrySymbol`, and `[Section]` on a table of function pointers | [modules-build.md](modules-build.md#properties) |
| linker scripts and flags | `LinkerScript`, `LinkerArgs` | [modules-build.md](modules-build.md#properties) |
| vendor SDKs and HALs | imported as any C library, compiled from `CSource` items | [c-interop.md](c-interop.md) |

## Inline assembly

`Asm` is an intrinsic that becomes GNU extended assembly, `__asm__ volatile`, written in C# as a
template string and typed operand lists for outputs, inputs and clobbers. Its exact signature is
designed before M9. On MSVC, which has no inline assembly on x64, `Asm` is `CD0002` and
`AsmSource` items are assembled by the toolchain's assembler.

## SIMD

`System.Runtime.Intrinsics.Vector128<T>` and its siblings keep their .NET names and meaning
(D002): fixed-size vectors of primitives with element-wise operators. Each is the C compiler's
vector type of that size, so the imported intrinsic headers (`<immintrin.h>`, `<arm_neon.h>`)
take and return them instead of being `CD6003`. Their full surface is designed before M9.
