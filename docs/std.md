# Standard library

The std is a CΔ package shipped with the compiler. It is minimal and lazy: where a C library
already solves a problem well, the std wraps it instead of reimplementing it.

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

## First version

| Module | Contents | Built on |
|---|---|---|
| `Std.Mem` | `Copy`, `Set`, `Eql`, `IndexOf`, `StartsWith`, `Swap`, `IAllocator`, `AllocError`, `Create<T>`, `Destroy<T>`, `AllocSpan<T>`, `FreeSpan<T>`, `Dupe<T>` | `memcpy`, `memset`, `memcmp` through the C compiler's builtins |
| `Std.Heap` | `PageAllocator`, `CAllocator` (`malloc`, `realloc`, `free`), `FixedBufferAllocator`, `ArenaAllocator`, `DebugAllocator` | the OS page API, libc |
| `Std.Collections` | `List<T>` (growable array, `Items` is a `Span<T>`), `HashMap<K, V, THash>` with `IHasher<K>`, `Deque<T>` | itself |
| `Std.Text` | UTF-8 validation and iteration, `Ascii`, parsing and formatting of integers and floats | itself; float formatting and parsing wrap a C implementation |
| `Std.Fmt` | `IWriter`, interpolated string handlers that format into a writer or a buffer, the `{value:spec}` format specifiers | itself, with comptime `TypeInfo` for structs and enums |
| `Std.Io` | `IReader`, `IWriter`, `BufferedReader<T>`, `BufferedWriter<T>`, standard streams | `read`, `write`; Win32 handles |
| `Std.Fs` | `File`, `Dir`, open, read, write, stat, iterate, paths | POSIX through libc; Win32 |
| `Std.Process` | `Args()`, `Env`, `Exit`, child processes | libc; Win32 |
| `Std.Time` | monotonic and wall clocks, `Sleep` | `clock_gettime`; Win32 |
| `Std.Thread` | `Thread`, `Mutex`, `Condition`, `Once`, `Atomic<T>` | C11 `<threads.h>` or pthreads; Win32 |
| `Std.Math` | the functions of `<math.h>`, checked and wrapping integer helpers with explicit overflow results | libm and compiler builtins |
| `Std.Debug` | `Print` to standard error, `Assert`, stack traces where the platform gives them | `Std.Io`, libc `backtrace` where available |
| `Std.Testing` | `Expect`, `ExpectEqual`, `ExpectError`, `Allocator` | `Std.Debug` |
| `Std.C` | helpers for C interop: NUL-terminated text, `Flexible<T>`, `errno` | libc |

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

## Later

Networking, a JSON reader and writer, compression and hashing wrappers (zlib, xxhash),
`Std.Build` for build steps in CΔ, SIMD vectors. Each enters the std only when a real program
needs it.
