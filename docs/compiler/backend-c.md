# C backend

`CDelta.Backend.C` writes `Final` Delta IR as C11. The generated C is not meant to be edited, but
it is meant to be read: names are recognizable and `#line` maps every statement to its CΔ source.

## Output

| File | Contents |
|---|---|
| `<PackageName>.c` | the whole program as one translation unit |
| `<PackageName>.libc.c` | the two functions that need libc headers, when libc is linked and the program can panic or return an error from `Main` |
| `<PackageName>.h` | the export header, when there are exports ([../language/c-interop.md](../language/c-interop.md#generated-header)) |

One translation unit for the whole program keeps the backend simple and gives the C compiler
whole-program optimization. Splitting into several units is a later optimization of build time,
not of the design.

The program's translation unit includes no header that declares functions, other than the user's
imports. A `[DllImport]` prototype is written with the IR's types (`int32_t puts(uint8_t*)`),
which conflicts with a libc header's declaration of the same name (`int puts(const char*)`), so
what the prelude needs from libc lives in `<PackageName>.libc.c`:

```c
#include <stdio.h>
#include <stdlib.h>
void cd_libc_write_stderr(const uint8_t* bytes, size_t length) { fwrite(bytes, 1, length, stderr); }
noreturn void cd_libc_abort(void) { abort(); }
```

The program's unit declares these two prototypes and does everything else itself: the panic and
trace texts, including line numbers, are formatted in plain C and written with
`cd_libc_write_stderr`. Float functions, atomics and traps use compiler builtins
(`__builtin_fmod`, `__atomic_*`, `__builtin_trap`), never `<math.h>`, `<stdatomic.h>` or
`<threads.h>`.

A `[DllImport]` function whose C name an imported header also declares gets no prototype: calls go
through the header's declaration, with arguments cast as for any imported function. Two
`[DllImport]` declarations of one C name must have identical IR signatures (`CD6010`).

GCC knows libc functions as built-ins and warns when a prototype differs from the built-in's
type (`-Wbuiltin-declaration-mismatch`), even when the ABI is the same. When the unit has
`[DllImport]` prototypes, they are preceded by:

```c
#if defined(__GNUC__) && !defined(__clang__)
#pragma GCC diagnostic ignored "-Wbuiltin-declaration-mismatch"
#endif
```

so the generated C compiles under `-Werror` with every toolchain and needs no extra flag.

The translation unit is ordered:

1. `#include <stdint.h>`, `<stddef.h>`, `<stdbool.h>`, `<stdalign.h>`, `<stdnoreturn.h>`, which
   declare no functions.
2. The prelude: only the helpers the program uses (overflow arithmetic, saturating conversions,
   the default panic handler, `alloca`).
3. The C imports: their defines and `#include` lines, in import order.
4. Forward declarations of every struct.
5. Type definitions in dependency order.
6. Layout assertions.
7. Prototypes of every function, and of every `[DllImport]` function.
8. Globals, with constant initializers.
9. Function definitions.

No external runtime file exists. Everything the program needs is in these generated files or in
libraries the program chose to link.

## Names

| What | C name |
|---|---|
| exported symbol | the export name |
| imported C declaration | its C name, or its typedef name when the IR says so |
| `[DllImport]` function | its C name |
| other function or global | `cd_` + path segments joined by `_`, characters outside `[A-Za-z0-9]` written `_xHH` |
| functions sharing a `Name` | every one of them gets `__` + 8 hex digits of a hash of its `Key` |
| generic instance | + `__` + 8 hex digits of a hash of its instance key, written as in the text format (`Key<targs>`) |
| type | the same scheme with `cd_T_` |
| local | its source name if it is a valid C identifier, not a C keyword and not shadowing; otherwise `l<n>`; temps `t<n>` |

Hashes use a fixed algorithm over the UTF-8 bytes of the key: FNV-1a 64 bits, then the high 32
bits XOR the low 32 bits, printed as 8 lowercase hex digits. Names are therefore stable across runs
and machines, and adding an unrelated function never renames another.

## The prelude

Every helper the prelude can contain, with its exact name. A helper is emitted only when the
program uses it; each is `static` (`static inline` for arithmetic).

| Helper | Contract |
|---|---|
| `noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)` | calls the user's `[PanicHandler]` if there is one, else `cd_default_panic` |
| `noreturn void cd_default_panic(...)` | same parameters; the output below through `cd_libc_write_stderr`, then `cd_libc_abort()`; without libc `__builtin_trap()` |
| `cd_trace`, `cd_trace_len` | `_Thread_local` ring of 32 `{ const char* file; int32_t line; }` and its count |
| `void cd_trace_push(const char* file, int32_t line)` | writes at `cd_trace_len % 32`, then increments |
| `void cd_trace_print(void)` | `intrinsic trace.print`: the trace block below, when `cd_trace_len > 0` |
| `bool cd_text_eql(cd_T_slice_const_uint8_t a, cd_T_slice_const_uint8_t b)` | `intrinsic text.eql`: equal lengths and `__builtin_memcmp` |
| `int32_t cd_utf8_next(cd_T_slice_const_uint8_t s, int32_t* offset)` | `intrinsic utf8.next`: decodes one scalar at `*offset` and advances past it; an invalid or truncated sequence gives `0xFFFD` and advances by one |
| `void cd_report_error(const uint8_t* name, int32_t len)` | `intrinsic error.report`: writes `error: `, the name and a newline through `cd_libc_write_stderr`, then calls `cd_trace_print` |
| `<to> cd_f2i_wrap_<from>_<to>(<from> v)` | saturating conversion, `NaN` to `0` |
| `<to> cd_f2i_trap_<from>_<to>(<from> v, const char* file, int32_t line)` | panics "float cast out of range" when not representable |
| `cd_alloca(n, a)` | a macro, since the memory must belong to the caller's frame: `n` bytes aligned to `a`, through `__builtin_alloca` (`_alloca` on MSVC) of `n + a - 1` bytes with the address rounded up |
| `uint16_t cd_err_from_<E>(<E> v)`, `<E> cd_err_to_<E>(uint16_t code)` | one pair per enum used by `enum.error` or `error.enum`: a `switch` from each member's value to its error number and back |

`<from>` and `<to>` are the IR type names (`f64`, `i32`). Integer overflow checks are written in
place with `__builtin_add_overflow`, `__builtin_sub_overflow` and `__builtin_mul_overflow` and a
call to `cd_panic`, not through helpers. Every `check` calls `cd_panic` with the message of its
kind from [../language/memory.md](../language/memory.md#build-modes-and-illegal-behavior), exactly
as written there.

### Output of a panic

On standard error, through `cd_libc_write_stderr`:

```
panic: <message>
  at <file>:<line>
error return trace:
  <file>:<line>
```

The trace block appears only when the trace is not empty and lists the recorded entries oldest
first, at most the last 32. `<file>` is the display path of the source ([ir.md](ir.md#grammar)).

### Output of an error from `Main`

```
error: <Name>
error return trace:
  <file>:<line>
```

then the exit code `1`, written by `cd_report_error`. The error name table is always emitted when
`Main` returns a `Result`.

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
| `slice<T> const` | `typedef struct { const T* ptr; int32_t len; } cd_T_slice_const_<T>` |
| `array<T, N>` | `typedef struct { T e[N]; } cd_T_array_<N>_<T>`, for locals, globals, parameters, returns and fields of structs the backend defines |
| `array<T, N>` field of an imported struct | the header's `T name[N]`; copying it as a value goes through `__builtin_memcpy` into the wrapper |
| `fn(sig)` | `R (*)(params)` through a `typedef` |
| `closure(sig)` | `struct { R (*fn)(void* ctx, params); void* ctx; }` |
| `dyn<I>` | `struct { void* self; const cd_T_vtable_<I>* vt; }` |
| `optional<T>` | `typedef struct { T value; bool has; } cd_T_optional_<T>`; `payload(0)` is `value` |
| `result<T, E>` | `typedef struct { T value; uint16_t err; } cd_T_result_<T>`, shared by every error set; `uint16_t` alone when `T` is `void`; `haspayload` is `err == 0`, `err` reads the field |
| `errorset{...}`, `anyerror` | `uint16_t`; an `error` constant is its number from the `errorname` items; `error.error` casts are nothing |
| `struct` | `struct` with the fields in IR order and `_Alignas` where the IR alignment exceeds natural |
| explicit layout | `union` of one `struct` per field, each with a leading `char` padding array of the field's offset; a member struct whose field offset is not a multiple of the field's alignment is packed (`__attribute__((packed))`, `#pragma pack(1)` on MSVC), and the union has `_Alignas` of the IR alignment |
| `union` (tagged) | `struct { tag_t tag; union { ... } as; }` |
| `enum` | a `typedef` of the underlying integer; members are written as typed literals at each use |
| `opaque` | an incomplete `struct` |
| `void` local or field | omitted |

In these names, `<T>` is the C spelling of the element type with `*` written `_p`, spaces written
`_` and any other character outside `[A-Za-z0-9_]` written `_xHH`: `cd_T_slice_const_uint8_t`,
`cd_T_array_4_cd_T_Hello_Vec2`, `cd_T_slice_int32_t_p`, `cd_T_optional_int32_t`.

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
- A constant `stackalloc` is already an array local in the IR. `alloca T, n, init` is
  `(T*)cd_alloca(n * sizeof(T), _Alignof(T))`, never a variable-length array, followed for `zero`
  by `__builtin_memset` with `0` and for `fill` with `0xAA`; `undef` writes nothing. The byte
  count is computed in `size_t` without a check: a count that overflows it is illegal behavior,
  like any stack overflow.
- `slice(place, a, b)` is `{ base + a, b - a }`, where `base` is the slice's `ptr` or the array's
  `e`.

### Arithmetic

C's integer promotions differ from the IR's typed operations, so every operand is cast to the IR
operation's type and every result back to it:

| Mode | Emitted |
|---|---|
| `wrap` | the operation on the unsigned type of the same width, then converted to the result type |
| `trap` | `__builtin_add_overflow` and its siblings on GCC and Clang; explicit range tests on MSVC and tcc; failure calls the panic handler |
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
- Calls of a function with linkage `c.macro` are written as a call of the macro name, as the bare
  name for a function with no parameters and the `template` form, or as the template with its
  arguments and the C spellings of the instance's type arguments substituted. A `ref` argument is written as the lvalue it points to, `(*t)`,
  and a call returning `void` as a statement.
- A variadic call passes the variadic arguments as written; C's default argument promotions apply,
  which is the C meaning.

### Atomics and threads

`Interlocked` and `Volatile` map to `__atomic_*` builtins on GCC, Clang and tcc and to the
`Interlocked` intrinsics on MSVC, both of which work on ordinary objects, unlike `_Atomic` types.
tcc defines the `__ATOMIC_*` orders only in its `<stdatomic.h>`, so a program that uses atomics
includes it there, under `#if defined(__TINYC__)`.
`_Thread_local` implements `[ThreadStatic]`.

## Debug information

In Debug and ReleaseSafe, each statement is preceded by `#line <line> "<file>"`, so the C
compiler's debug information points at the CΔ source. Locals and fields keep their source names,
so a debugger shows them. ReleaseFast and ReleaseSmall omit `#line` unless `DebugSymbols` is set.

When `DebugSymbols` is set, the build also writes debugger formatters generated from the IR
(D159), next to the output:

| File | Debugger |
|---|---|
| `<PackageName>.gdb.py`, loaded through a `.debug_gdb_scripts` section | GDB |
| `<PackageName>.lldb.py`, loaded by the generated launch configuration | LLDB |
| `<PackageName>.natvis` and `<PackageName>.natstepfilter` | Visual Studio and the MSVC debugger |

They show `string` as its text, a slice as its elements, `result` as `Ok(value)` or
`Err(Name)` with the error's name, `optional` as `None` or its value, a `[Union]` as its active
member, an exhaustive enum by member name, and every type and function by its CΔ name; they make
stepping skip the prelude helpers.

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

`EmitC=true` or `cdc build --emit-c` keeps `<PackageName>.c`, `<PackageName>.libc.c` and
`<PackageName>.h`, with a `CMakeLists.txt` that declares
`project(<PackageName> VERSION <Version> LANGUAGES C)` (`C CXX` with `CppSource` items), one
`add_library(<PackageName> ...)` of the generated and `CSource` files whose kind follows
`BUILD_SHARED_LIBS`, the alias `<PackageName>::<PackageName>` for `FetchContent`, the header's
directory as a `PUBLIC` include directory, `C_STANDARD 11` with `C_STANDARD_REQUIRED`,
`C_VISIBILITY_PRESET hidden`, the `NativeLibrary` items as link libraries, and no compile
definitions: the build mode is already in the generated C. Together they build with any C11 compiler for the same target with no other file, which is how CΔ code is handed
to a C project that does not use `cdc`.
