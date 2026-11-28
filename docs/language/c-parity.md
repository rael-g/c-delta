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
| `__builtin_frame_address`, `__builtin_return_address` | `FrameAddress()`, `ReturnAddress()` intrinsics, for stack walking and conservative collectors | this document |
| a guaranteed tail call (`__attribute__((musttail))`) | `return MustTail(F(x));` | this document |
| C++ sources behind an `extern "C"` boundary | `CppSource` items, compiled by the toolchain's C++ compiler and linked with its C++ library | [modules-build.md](modules-build.md#items) |
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

## Alignment, aliasing and interrupts

- `[Align(n)]` gives a struct, a field or a static field a minimum alignment. On a field it moves
  the field's offset to the next multiple of `n` and raises the struct's alignment to at least
  `n`. `n` is a power of two, and under explicit layout the given offset is a multiple of it
  (`CD6014`). An `n` at or below the natural alignment changes nothing, and C gets no `_Alignas`,
  which C rejects when weaker than natural.
- `[NoAlias]` marks a pointer parameter as the only way the function reaches that memory; the C
  parameter is `restrict`. A `restrict` parameter of an imported function is kept the same way.
- `[Interrupt(kind)]` marks a `static void` method without parameters as an interrupt handler:

  | `InterruptKind` | Targets | C |
  |---|---|---|
  | `Irq` | `thumbv6m-none-eabi`, `thumbv7em-none-eabi`, `thumbv7em-none-eabihf` | `__attribute__((interrupt("IRQ")))` |
  | `Machine` | `riscv32imac-none-elf` | `__attribute__((interrupt("machine")))` |
  | `Supervisor` | `riscv32imac-none-elf` | `__attribute__((interrupt("supervisor")))` |

  A kind the target lacks, any kind on another target, or another signature is `CD6013`. A
  handler saves the length of the error return trace on entry and restores it on every exit.

## Language runtimes

These capabilities are what a language runtime needs: a collector scanning stacks, coroutines
switching them, an interpreter dispatching by tail calls. They let a frontend for another
language compile that language's runtime from its own sources, together with the program
(D169). `FrameAddress`, `ReturnAddress` and `MustTail` are designed with `Asm` before M9;
`MustTail` is `CD0002` on a toolchain without guaranteed tail calls.

## SIMD

`System.Runtime.Intrinsics.Vector128<T>` and its siblings keep their .NET names and meaning
(D002): fixed-size vectors of primitives with element-wise operators. Each is the C compiler's
vector type of that size, so the imported intrinsic headers (`<immintrin.h>`, `<arm_neon.h>`)
take and return them instead of being `CD6003`. Their full surface is designed before M9.
