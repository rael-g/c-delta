# Core passes

`CDelta.Core` takes `Lowered` IR from the frontends and produces `Final` IR for the backends. It
knows no source and no target language. Optimization is deliberately minimal: the C compiler
optimizes; the core only does what the language semantics require and what keeps the output
small.

## Order

| # | Pass | Stage after |
|---|---|---|
| 1 | Roots | Lowered |
| 2 | Instantiation, with comptime and folding interleaved | Instantiated |
| 3 | Unions and dispatch tables | Instantiated |
| 4 | Error codes | Instantiated |
| 5 | Entry point and test runner | Instantiated |
| 6 | Layout | Instantiated |
| 7 | Safety | Final |
| 8 | Debug instrumentation | Final |
| 9 | Reachability cleanup | Final |
| 10 | Verify | Final |

In compiler Debug builds the verifier also runs between every pair of passes.

## 1. Roots

The roots of a build are: `Main` (executables), every `[Export]`, every `[Test]` (test builds),
the `[PanicHandler]`, and the initializers of globals reached from them. A library with no
exports and no tests compiles nothing beyond type checking.

## 2. Instantiation

A worklist of **instances**. An instance key is a function and its type arguments and comptime
arguments; two keys are equal when their arguments are structurally equal. For each new instance:

1. Get the generic body from the frontend (`IBodyProvider.Lower`, once per function).
2. Substitute type parameters and comptime parameters.
3. Resolve `constrained` callees through the `Impl` of the concrete type.
4. Evaluate comptime regions and intrinsics whose operands are now known (section 2a).
5. Fold constants, then remove branches whose condition is a constant. Calls in removed branches
   are never instantiated, which is what makes platform code and `CompileError` work.
6. Enqueue every callee instance and every global reached.

Depth of nested generic instantiation is limited to 64 (`CD5005`).

### 2a. Comptime interpreter

An interpreter over IR, inside the core, used for comptime regions, static initializers and
`[Comptime]` arguments.

- **Memory.** Every allocation (a global, a stack frame local, a comptime heap block) is a byte
  array laid out with the target's layout. A pointer is an allocation and an offset, never an
  integer. Reading bytes never written gives `undef`, and using `undef` is `CD4007`.
- **Layout on demand.** The interpreter asks the layout pass (6) for the layout of any concrete
  type it touches; layouts are memoized, so pass 6 reuses them.
- **Semantics.** Arithmetic in every mode behaves as at run time on the target. Every check of
  the safety pass applies regardless of build mode, and failing it is `CD4007`.
- **Limits.** The branch quota of [../language/comptime.md](../language/comptime.md) counts
  backward branches and calls. Calls to `extern` functions, `volatile`, atomics, thread-local
  storage and pointer to integer casts are `CD4001` or `CD4010`.
- **Memoization.** A comptime call with the same function and argument values returns the
  memoized result, as comptime functions are pure with respect to run-time state.
- **Materialization.** A result is converted to an IR constant by walking the memory reachable
  from it. Comptime heap blocks become new internal globals; pointers into a frame are `CD4009`;
  comptime-only values are `CD4005`.
- **Reflection.** `TypeInfo.Of<T>()` is answered from IR type declarations and layouts; attribute
  arguments from IR attributes.
- **Embed.** `Embed(path)` reads the file once per build, relative to the source file of the call,
  refusing paths outside the package root (`CD4008`).

## 3. Unions and dispatch tables

- Each `Union` gets its tag type (the smallest unsigned integer that counts its members), its
  payload size and alignment.
- Calls of methods on a union value become a `switch` on the tag calling each member's method.
- Each `Impl` used by `dyn.make` gets a table global: one function pointer per interface method,
  in interface declaration order, with thunks adapting `this` to `void*`.

## 4. Error codes

Collect the member names of every reachable `errorset` type and `error` constant; sort them
ordinally; number them from `1`; add one `errorname` item per name. More than 65535 names is
`CD3010`. If an `intrinsic error.name` is reachable, emit the name table global, a
`slice<u8> const` per code with index `0` unused, and replace each `error.name(e)` with the
element at `errcode e`. Every `cast error.error` is checked here: a target set that lacks a name
of the source set is a compiler bug, since `CD3002` already rejected it in the source.

## 5. Entry point and test runner

- An executable gets a synthetic C `main(int argc, char** argv)`: it stores `argc` and `argv` in
  the synthetic globals `synthetic:argc` and `synthetic:argv`, which the intrinsics
  `Startup.ArgCount()` and `Startup.ArgValues()` read and `Std.Process.Args()` wraps, calls `Main`, and converts its result into the exit code
  of [../language/errors.md](../language/errors.md#main). A freestanding target gets no `main`.
- A test build gets a synthetic `Main` that iterates over the tests, sorted by name, prints each
  name and result, and returns the failure count.

## 6. Layout

For every concrete struct, union, optional, result, slice, closure and dyn type:

| Layout | Rule |
|---|---|
| `c` | the target C ABI rules: fields in order, each at the next multiple of its alignment, size rounded to the struct alignment |
| `c.packed(n)` | the same with alignments capped at `n` |
| `auto` | fields sorted by decreasing alignment, stable, then the `c` rule |
| `explicit` | the given offsets; size is the maximum end rounded to the maximum alignment |
| bit-fields | the target ABI's bit-field rules (SysV or Microsoft), both implemented |

### Target data

The core describes each target with a fixed table in `CDelta.Core`; nothing is queried from a C
compiler. Every primitive has its size as alignment except where stated.

| Target | Pointer | `long` | `wchar_t` | `char` | `i128`, `u128` | `f16` | Bit-fields |
|---|---|---|---|---|---|---|---|
| `x86_64-linux-gnu`, `x86_64-linux-musl`, `x86_64-freestanding-none` | 8 | 8 | `i32` | signed | native, align 16 | native | SysV |
| `aarch64-linux-gnu`, `aarch64-freestanding-none` | 8 | 8 | `u32` | unsigned | native, align 16 | native | SysV |
| `aarch64-macos-none` | 8 | 8 | `i32` | signed | native, align 16 | native | SysV |
| `x86_64-windows-gnu` | 8 | 4 | `u16` | signed | native, align 16 | native | Microsoft |
| `x86_64-windows-msvc` | 8 | 4 | `u16` | signed | struct of two `u64`, align 8 | struct of `u16` | Microsoft |
| `wasm32-wasi-musl` | 4 | 4 | `i32` | signed | struct of two `u64`, align 8 | struct of `u16` | SysV |

All targets are little endian. The layout catalog test of [testing.md](testing.md#layouts) checks
this table against clang; a difference is a bug in the table.

Imported C types keep clang's layout. Every computed layout is asserted against the C compiler by
the backend, so a mistake here fails the C build instead of corrupting memory.

## 7. Safety

The build mode and `[Safety]` overrides decide, per function, whether checks are on. Then:

- `default` arithmetic and conversion modes become `trap` when checks are on, `assume` when off.
  `checked` stays `trap`; `wrap` stays `wrap`.
- `check` statements are inserted:

| Check kind | Before |
|---|---|
| `bounds` | element access of slices and arrays (`0 <= i < len`); `slice` rvalues (`0 <= start <= end <= len`); an `aggregate` of a slice type (`len >= 0`) |
| `null` | dereference of a nullable pointer; calls through function pointers, closures and dyn values |
| `optional` | every read of `payload(0)` of an optional, `haspayload` true; none is elided |
| `result.value`, `result.error` | every read of `payload(0)` of a result (success) and every `err` (error), including those `Try`, `Catch` and `TryGet` emit after their own branch; the C compiler folds the repeated test |
| `enum` | integer to exhaustive enum conversion in `trap` mode |
| `align` | pointer casts to a type of larger alignment |
| `divzero` | integer division and remainder, in every mode, checks on or off |
| `overlap` | `Std.Mem.Copy` (an intrinsic of the std) |

- `unreachable` becomes `trap` with checks on and `assume` with checks off.

## 8. Debug instrumentation

With checks on: `fill.undef` after `Unsafe.SkipInit` and after the array local of a constant
`stackalloc` under `[SkipLocalsInit]`, and `alloca` with `undef` becomes `alloca` with `fill`. The
frontend emits every `trace.push`, `trace.save` and `trace.restore`; this pass keeps them when
`ErrorReturnTrace` is enabled and removes them otherwise.

## 9. Reachability cleanup

Remove every function, global, type, table and constant not reachable from the roots after all
the passes above. The output contains only what the program uses.

## What the core does not do

No inlining, no loop optimization, no register allocation, no instruction selection, no escape
analysis beyond the frontend's checks. These belong to the C compiler or to a future backend.
