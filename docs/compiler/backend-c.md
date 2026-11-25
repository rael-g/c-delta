# C backend

`CDelta.Backend.C` writes `Final` Delta IR as C11. The generated C is not meant to be edited, but
it is meant to be read: names are recognizable and `#line` maps every statement to its CΔ source.

## Output

| File | Contents |
|---|---|
| `<PackageName>.c` | the whole program as one translation unit |
| `<PackageName>.h` | the export header, when there are exports ([../language/c-interop.md](../language/c-interop.md#generated-header)) |

One translation unit for the whole program keeps the backend simple and gives the C compiler
whole-program optimization. Splitting into several units is a later optimization of build time,
not of the design.

The translation unit is ordered:

1. `#include <stdint.h>`, `<stddef.h>`, `<stdbool.h>`, `<stdalign.h>`, `<stdnoreturn.h>`; and
   `<stdatomic.h>`, `<threads.h>`, `<math.h>` only if used.
2. The prelude: only the helpers the program uses (overflow arithmetic, saturating conversions,
   the default panic handler, `alloca`).
3. The C imports: their defines and `#include` lines, in import order.
4. Forward declarations of every struct.
5. Type definitions in dependency order.
6. Layout assertions.
7. Prototypes of every function, and of every `[DllImport]` function.
8. Globals, with constant initializers.
9. Function definitions.

No external runtime file exists. Everything the program needs is in this translation unit or in
libraries the program chose to link.

## Names

| What | C name |
|---|---|
| exported symbol | the export name |
| imported C declaration | its C name, or its typedef name when the IR says so |
| `[DllImport]` function | its C name |
| other function or global | `cd_` + path segments joined by `_`, characters outside `[A-Za-z0-9]` written `_xHH` |
| overload | + `__` + 8 hex digits of a hash of its documentation comment ID |
| generic instance | + `__` + 8 hex digits of a hash of its instance key |
| type | the same scheme with `cd_T_` |
| local | its source name if it is a valid C identifier, not a C keyword and not shadowing; otherwise `l<n>`; temps `t<n>` |

Hashes use a fixed algorithm (FNV-1a 64 bits folded to 32) over the canonical text of the key, so
names are stable across runs and machines.

## Types

| IR | C |
|---|---|
| `bool` | `bool` |
| `iN`, `uN` for 8 to 64 | `intN_t`, `uintN_t` |
| `i128`, `u128` | `__int128`, `unsigned __int128` where the compiler has them; otherwise a struct of two `uint64_t` with helper functions |
| `isize`, `usize` | `intptr_t`, `uintptr_t` |
| `f16` | `_Float16` where available, otherwise a struct of `uint16_t` converted through helpers |
| `f32`, `f64` | `float`, `double` |
| `c.*` | the C spelling (`long`, `unsigned char`) |
| `ptr<T>` | `T*`, `const T*` for `const`, `volatile T*` for `volatile` |
| `slice<T>` | `typedef struct { T* ptr; int32_t len; } cd_T_slice_<T>` |
| `array<T, N>` inside a struct | `T name[N]` |
| `array<T, N>` as a value | a struct wrapping `T e[N]`, so it can be assigned and returned |
| `fn(sig)` | `R (*)(params)` through a `typedef` |
| `closure(sig)` | `struct { R (*fn)(void* ctx, params); void* ctx; }` |
| `dyn<I>` | `struct { void* self; const cd_T_vtable_<I>* vt; }` |
| `optional<T>` | `struct { T value; bool has; }` |
| `result<T, E>` | `struct { T value; uint16_t err; }`; `uint16_t` alone when `T` is `void` |
| `struct` | `struct` with the fields in IR order and `_Alignas` where the IR alignment exceeds natural |
| explicit layout | `union` of one `struct` per field, each with a leading `char` padding array of the field's offset |
| `union` (tagged) | `struct { tag_t tag; union { ... } as; }` |
| `enum` | a `typedef` of the underlying integer; members are written as typed literals at each use |
| `opaque` | an incomplete `struct` |
| `void` local or field | omitted |

### Layout assertions

For every struct the backend defines, and every imported struct the program uses:

```c
_Static_assert(sizeof(cd_T_Hello_Vec2) == 8, "layout of Hello.Vec2");
_Static_assert(_Alignof(cd_T_Hello_Vec2) == 4, "layout of Hello.Vec2");
_Static_assert(offsetof(cd_T_Hello_Vec2, Y) == 4, "layout of Hello.Vec2.Y");
```

These make the C compiler verify the core's layouts (and, for imported structs, the C frontend's
reading of clang), on every build.

## Bodies

- Every local is declared at the top of the function; `live` and `dead` become nothing in C
  except where Debug requires a fill.
- Every block is a label; terminators are `goto`, `if (...) goto ...; else goto ...;`, `switch`
  with `goto` in each case, `return`, and the unreachable and panic forms below.
- The IR already sequences every operation through temps, so each C statement has at most one
  side effect and C's unspecified evaluation order never matters.
- `stackalloc` of a constant size is a local array; of a run-time size, `alloca` from the prelude
  (`__builtin_alloca`, or `_alloca` on MSVC), never a variable-length array.

### Arithmetic

C's integer promotions differ from the IR's typed operations, so every operand is cast to the IR
operation's type and every result back to it:

| Mode | Emitted |
|---|---|
| `wrap` | the operation on the unsigned type of the same width, then converted to the result type |
| `trap` | `__builtin_add_overflow` and its siblings on GCC and Clang; explicit range tests on MSVC; failure calls the panic handler |
| `assume` | the plain C operation; signed overflow is undefined in C, which is exactly the meaning |

- Shift counts are masked to the operand width, as the language requires.
- Division and remainder are guarded against zero, and signed division against `MIN / -1`.
- Float to integer conversions go through prelude helpers in `trap` and `wrap` (saturating); in
  `assume` they are plain casts.
- Conversion of an out-of-range unsigned value to a signed type is implementation-defined in C; the
  prelude asserts at compile time that the compiler uses two's complement, which every supported
  toolchain does.

### Checks, panics and unreachable

```c
if (__builtin_expect(!(i < s.len), 0)) cd_panic_bounds(i, s.len, "Program.cs", 12);
```

- `check` becomes a test calling a `noreturn` panic helper with the message and source location.
- `panic` calls the panic handler: the user's `[PanicHandler]`, or the prelude default.
- `unreachable(trap)` calls the panic handler; `unreachable(assume)` is `__builtin_unreachable()`
  or `__assume(0)`.

### Calls

- Arguments are already temps. Structs are passed and returned by value; `ref`, `out` and `in`
  are pointers.
- `closure` calls pass the context first. `dyn` calls load the function from the table and pass
  `self` first.
- `c.macro` calls are written as a call of the macro name.
- A variadic call passes the variadic arguments as written; C's default argument promotions apply,
  which is the C meaning.

### Atomics and threads

`Interlocked` and `Volatile` map to `__atomic_*` builtins on GCC and Clang and to the `Interlocked`
intrinsics on MSVC, both of which work on ordinary objects, unlike `_Atomic` types.
`_Thread_local` implements `[ThreadStatic]`.

## Debug information

In Debug and ReleaseSafe, each statement is preceded by `#line <line> "<file>"`, so the C
compiler's debug information points at the CΔ source. Locals keep their source names, so a
debugger shows them. ReleaseFast and ReleaseSmall omit `#line` unless `DebugSymbols` is set.

## C compiler flags

| Flag | Why |
|---|---|
| `-std=c11` | the generated C is C11, with compiler extensions guarded by feature tests |
| `-fno-strict-aliasing` | CΔ allows reinterpreting memory through pointers, as C# does |
| `-ffp-contract=off` | no fused multiply-add contraction, as C# |
| `-fwrapv` is **not** used | `assume` relies on signed overflow being undefined |
| `-w` | the generated C is not the user's code; CI compiles it with `-Wall -Wextra -Werror -pedantic` instead |

| Mode | Flags |
|---|---|
| Debug | `-O0 -g` |
| ReleaseSafe | `-O2 -g` |
| ReleaseFast | `-O3` |
| ReleaseSmall | `-Os` |

MSVC receives the equivalent options (`/std:c11`, `/Od`, `/O2`, `/O1`, `/Zi`, `/fp:precise`).

## Delivering C

`EmitC=true` or `cdc build --emit-c` keeps `<PackageName>.c` and `<PackageName>.h`. Together they
build with any C11 compiler for the same target with no other file, which is how CΔ code is handed
to a C project that does not use `cdc`.
