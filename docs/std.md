# Standard library

The std is a CΔ package shipped with the compiler. It is not a BCL, but it closes the largest
gaps the absence of one leaves, and it is lazy: where a C library already solves a problem well
and fits CΔ's rules, the std projects it instead of reimplementing it (D147).

## Rules

- Every function that allocates takes an `IAllocator` (or a type parameter constrained to it).
- Every function that can fail returns a `Result`; nothing in the std panics on bad input, only on
  illegal behavior.
- Everything is in namespace `Std`, one static class or struct per concept.
- Wrapping is an implementation detail: the public surface of the std speaks CΔ (spans, `Result`,
  allocators), never raw C types.
- What needs the OS works through libc on targets that link it, and through the OS API where
  libc is absent or worse (Windows uses Win32 directly).
- The std follows the same subset rules as user code; it gets no exemption.
- From 1.0, the std and the corelib only grow; before 1.0 they change freely. A build has one of each, shared by packages of every edition, so nothing in it
  is removed or changed incompatibly: a member that ages gets `[Obsolete]` naming its
  replacement, and an incompatible change is a new member (D161).

## What is CΔ and what is projected

A module is written in CΔ when any of these holds:

- it must work at comptime (the interpreter runs no C bodies before M9);
- it must work on freestanding targets, where there is no libc;
- it is generic: a CΔ `List<T>` is monomorphized and typed, a C container is `void*`.

Otherwise it may be built on a C library, which the std vendors as `CSource` items compiled by the
same toolchain, with the same target and mode, and links statically. A C library qualifies only
when all of these hold:

| Criterion | Why |
|---|---|
| a permissive license (MIT, BSD, ISC, zlib, Apache-2.0, public domain); never GPL or LGPL | the license of a static library binds the user's program |
| it allocates nothing, or through hooks given per object or context, so each maps to an `IAllocator` | every function that allocates takes an allocator; a process-wide allocator hook does not qualify |
| no hidden mutable global state, or the projection can confine it completely | locale, static buffers and global initialization change behavior at a distance |
| errors come back as return values, never through `abort`, `exit` or `longjmp` | they become `Result` |
| reentrant per context | `Std.Thread` makes no global lock |
| it builds as C99 or C11 from a list of files on every tier 1 target, without its own build system | the std is built by `cdc`, from pinned sources |

A library that fails a criterion is not part of the std. A program that accepts its defects uses
it directly with `[Import]`, which is already zero cost.

A projected module is the projection's output over the library's headers (errno and
status codes to `Result`, pointer and length pairs to spans, allocation hooks to `IAllocator`),
plus a thin hand-written layer where the semantics need it. Its public surface speaks CΔ only.

## First version

| Module | Contents | Built on |
|---|---|---|
| `Std.Mem` | `Copy`, `Set`, `Eql`; extension members on `Span<T>` and `ReadOnlySpan<T>` with the meaning of C#'s span extensions: `IndexOf` and `LastIndexOf` of an element or a span (the element index, `-1` when absent), `StartsWith`, `EndsWith`; `Swap`, `Swap`, `IAllocator`, `AllocError`, `Create<T>`, `Destroy<T>`, `AllocSpan<T>`, `FreeSpan<T>`, `Dupe<T>` | `memcpy`, `memset`, `memcmp` through the C compiler's builtins |
| `Std.Heap` | `PageAllocator`, `CAllocator` (`malloc`, `realloc`, `free`), `FixedBufferAllocator`, `ArenaAllocator`, `VirtualArenaAllocator` ([below](#virtual-arena)), `DebugAllocator` | the OS page API, libc |
| `Std.Collections` | `List<T>` (growable array, `Items` is a `Span<T>`), `Dictionary<TKey, TValue>` (a value indexer, `GetValueRef` for updates in place) and `HashSet<T>` (hashing chosen at comptime from the key type: primitives, `string`, enums, structs field by field), `Dictionary<TKey, TValue, THasher>` and `HashSet<T, THasher>` with an `IHasher<T>`, `Queue<T>`, `Stack<T>`, `Pool<T>` (slots addressed by a `Handle<T>` of index and generation; a stale handle is an error value, not a dangling pointer); every growing operation takes the allocator given at construction and returns a `Result` | itself |
| `Std.Text` | `StringBuilder` (a struct over an allocator, with an interpolated string handler for `Append($"...")`, `ToString()` giving a `string` over its buffer); extension members on `string`: `StartsWith`, `EndsWith`, `Contains`, `Split`, `SplitOnce`, `Trim`, `EqualsIgnoreCase`, `EnumerateRunes()` (the enumerator of `Rune` the `Std.Linq` operators take); `FromUtf8` validation; `Concat(allocator, ...)`, `ToUpper(allocator)`, `ToLower(allocator)`; static extensions `int.Parse(string)` and the other numeric types returning `Result`; `Ascii`; formatting of integers and floats | itself, since formatting runs at comptime |
| `Std.Fmt` | `IWriter`, interpolated string handlers that format into a writer or a buffer, the `{value:spec}` format specifiers | itself, with comptime `TypeInfo` for structs and enums |
| `Std.Io` | `IReader`, `IWriter`, `BufferedReader<T>`, `BufferedWriter<T>`, standard streams | `read`, `write`; Win32 handles |
| `Std.Fs` | `File`, `Dir`, open, read, write, stat, iterate, paths | POSIX through libc; Win32 |
| `Std.Process` | `Args()`, `Env`, `Exit`, `Run(Func<Result>)` and `Run(Func<Result<int>>)` giving the exit code of a failing `Main`, child processes | libc; Win32 |
| `Std.Linq` | lazy queries over spans and enumerators, each operator a struct holding its source and delegate: `Where`, `Select`, `SelectMany`, `Take`, `Skip`, `TakeWhile`, `SkipWhile`, `Zip`, `Concat`; terminals `Count`, `Any`, `All`, `Sum`, `Min`, `Max`, `Aggregate`, `First` and `Last` as optionals; those that need memory take an allocator: `ToList(allocator)`, `OrderBy` and `GroupBy`. Method syntax only; query expressions stay removed (`CD1014`) | itself |
| `Std.Time` | monotonic and wall clocks, `Sleep`, `Stopwatch`, `TimeSpan`, `DateTime` (UTC; time zones are later) | `clock_gettime`; Win32 |
| `Std.Random` | `Random`, a struct with an explicit seed; `Random.Shared`, one per thread, seeded from the OS | itself; the OS for seeding |
| `Std.Thread` | `Thread`, `Mutex`, `Condition`, `Once`, `Atomic<T>` | C11 `<threads.h>` or pthreads; Win32 |
| `Std.Math` | the functions of `<math.h>`, checked and wrapping integer helpers with explicit overflow results | libm and compiler builtins |
| `Std.Debug` | `Print` to standard error, `Assert`, stack traces where the platform gives them | `Std.Io`, libc `backtrace` where available |
| `Std.Testing` | `Expect`, `ExpectEqual`, `ExpectError`, `Allocator` | `Std.Debug` |
| `Std.C` | helpers for C interop: `CString`, `ToCString(string, allocator)`, `Flexible<T>`, `errno` | libc |

## The MVP subset

The MVP ([roadmap.md](roadmap.md#mvp)) ships only what needs no generics of its own, no interfaces
and no formatting:

| Member | Signature |
|---|---|
| `Std.C.CString` | `struct CString { public byte* Pointer; public ReadOnlySpan<byte> AsSpan(); }`, the bytes before the NUL |
| `Std.Process.Args` | `static ReadOnlySpan<CString> Args()`, `argv` as stored by the entry point; from M4 it returns `ReadOnlySpan<string>` |
| `Std.Process.Exit` | `static void Exit(int code)`, never returns |
| `Std.Debug.Print` | `static void Print(ReadOnlySpan<byte> text)`, to standard error |
| `Std.Mem.Eql` | `static bool Eql(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)` |
| `Std.Mem.Copy` | `static void Copy(Span<byte> destination, ReadOnlySpan<byte> source)`, lengths must be equal |
| `Std.Mem.Set` | `static void Set(Span<byte> destination, byte value)` |

Pointer types cannot be type arguments in C#, which is why `Args` returns `CString` values. Each
of these becomes generic or gains overloads in the first version without changing its behavior on
bytes.

Freestanding builds get `Std.Mem`, `Std.Heap.FixedBufferAllocator`, `Std.Collections`,
`Std.Text`, `Std.Fmt`, `Std.Math` (the parts without libm) and `Std.Testing`; modules that need an
OS fail to compile with `CompileError` when used.

## Formatting

```csharp
Print($"{name}: {count} items, {ratio:F2}\n");
```

`Print` takes a handler type of `Std.Fmt`. Roslyn lowers the interpolation into `AppendLiteral`
and `AppendFormatted<T>(T value, ReadOnlySpan<byte> format)` calls on the handler, which writes into
a fixed stack buffer and flushes to the writer: no allocation. `AppendFormatted<T>` dispatches on
`T` at comptime (primitives, spans of `byte` as text, enums by name, `[Union]` values by member,
structs field by field through `TypeInfo`).

The format of an interpolation hole is always literal, so the `format` parameter of
`AppendFormatted<T>` is `[Comptime]` (D188): a specifier `T` does not accept is `CD4004` at the
hole, and the specifier is parsed by comptime, never at run time.

The Unicode tables of `Std.Text` (general category, case mapping, display width) are
`static readonly` data that comptime computes from the Unicode data files in `lib/std`, read with
`Embed`, under a raised `[ComptimeQuota]` (D188).

### Virtual arena

`VirtualArenaAllocator` reserves a range of address space once and commits pages as its top
grows (D176). Memory is never moved, so pointers into it stay valid, and nothing is mapped until
it is used, so a reservation of gigabytes costs nothing.

| Member | Behavior |
|---|---|
| `static Result<VirtualArenaAllocator> Reserve(nuint bytes)` | reserves `bytes` rounded up to the page size (`mmap` with `PROT_NONE`, `VirtualAlloc` with `MEM_RESERVE`); failure is `AllocError.OutOfMemory` |
| `IAllocator` allocation | bumps the top by the aligned size, committing the pages it crosses (`mprotect`, `VirtualAlloc` with `MEM_COMMIT`); past the reservation or a failed commit is `AllocError.OutOfMemory` |
| `IAllocator` resize | grows or shrinks the most recent allocation in place; another allocation is moved by copy, as in `ArenaAllocator` |
| `IAllocator` free | moves the top back when it frees the most recent allocation, otherwise nothing |
| `VirtualArenaMark Mark()`, `Release(VirtualArenaMark mark)` | saves the top and moves it back to a saved one, freeing everything allocated since, in one store |
| `Reset()` | `Release` to the start; pages stay committed |
| `Trim()` | decommits the pages above the top (`madvise` with `MADV_DONTNEED`, `VirtualFree` with `MEM_DECOMMIT`) |
| `Dispose()` | releases the reservation |

It exists where `PageAllocator` does, except `wasm32-wasi-musl`, which has no address space to
reserve. `Mark` and `Release` with `Defer` give memory freed at the end of a scope; one arena per
growing buffer gives a buffer that grows without copying.

## Later

Automatic memory management is not part of the language or the std (D142). The std's answer to
"I do not want to free each object" is `ArenaAllocator` for memory freed together and `Pool<T>`
with handles for graphs and cycles; a shared owner that is truly needed is reference counted by
hand. A conservative collector may exist as a package outside the std, as an `IAllocator` over a
C library.

Flat data (D177): `Std.Mem.Rel<T>`, a 32-bit byte offset into a buffer resolved with a bounds
check, so a structure of nodes and their links is one copyable, position independent block that
is written or read with one copy; and a reader that checks such a block from untrusted bytes
through `TypeInfo` before handing it out.

Checked views as `ref struct` types (a scoped span over stack or arena memory), and a separate
analyzer package with the memory lints of [language/memory.md](language/memory.md#lifetimes),
outside the std and installed by choice.

Projected modules that close the largest BCL gaps, each over a library that meets the criteria
above:

| Module | Contents | Built on |
|---|---|---|
| `Std.Json` | reading into a document, writing, and reading into structs through `TypeInfo` | yyjson, with its per-document allocator |
| `Std.Compress` | deflate, gzip and zlib streams; zstd | zlib (`zalloc` and `zfree` per stream), zstd (custom memory per context) |
| `Std.Hash` | xxHash, CRC-32 | xxHash (one-shot and caller-owned states), zlib |
| `Std.Crypto` | SHA-2, BLAKE2b, HMAC, ChaCha20-Poly1305, X25519, Ed25519, secure random from the OS | BearSSL and Monocypher, neither of which allocates |
| `Std.Net` | sockets, addresses, name resolution; TLS client and server | POSIX sockets and Winsock; BearSSL |
| `Std.Regex` | compiled patterns, matching and captures | PCRE2, with a general context per pattern carrying its allocator |

`Std.Build` for build steps in CΔ and SIMD vectors are CΔ. A module enters the std when a real
program needs it, in the order of the table unless a program asks otherwise; a library is
replaced only by a decision.

Asset files as declarations (D187): a generator of `CDelta.Sdk.Generators` that turns the files a
project marks as assets into a static class with a member per file, its path or its `Embed`
contents, under a name derived from the file name.
