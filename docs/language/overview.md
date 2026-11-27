# Language overview

This is a tour. Every rule here is stated precisely in the document it links to.

## A program

```csharp
namespace Hello;

using Std.Heap;
using Std.Collections;
using static Std.Debug;

[Import("stdio.h")]
static unsafe partial class C { }

static class Program
{
    static Result Main()
    {
        var gpa = new DebugAllocator();
        Defer(() => gpa.Deinit());
        IAllocator allocator = Dyn<IAllocator>.Of(ref gpa);

        var squares = new List<int>(allocator);
        Defer(() => squares.Deinit());

        for (int i = 0; i < 10; i++)
            squares.Append(i * i).Try();

        foreach (var square in squares.Items)
            Print($"{square}\n");

        C.puts("done");
        return default;
    }
}
```

What to notice:

- `static class` is a module: a namespace for functions and globals. It has no instances.
- `[Import("stdio.h")]` reads the C header and fills the partial class with its declarations.
- `new DebugAllocator()` constructs a struct on the stack. `new` never allocates in CΔ.
- `Defer` runs its lambda body when the scope exits, in reverse order.
- `Dyn<IAllocator>.Of(ref gpa)` makes an interface value: a pointer to `gpa` plus a table of its
  methods. Nothing is copied or allocated.
- `Append` may fail because memory may run out; it returns `Result`, and `Try` returns the error
  to the caller if there is one.
- `Main` returns `Result`; an error becomes a non-zero exit code and a message.
- `$"{square}\n"` does not build a string; it is lowered to direct writes by a formatting handler.
- `"done"` is a UTF-8, NUL-terminated literal in static memory, passed to `puts` as `byte*`.

## The mental model

| You want | In CΔ | Document |
|---|---|---|
| A record of data | `struct` with public fields | [types.md](types.md) |
| A namespace of functions | `static class` | [subset.md](subset.md) |
| A method | an instance method of a struct; `this` is a reference | [types.md](types.md) |
| A single-item pointer that is never null | `ref T`, `ref readonly T`, `in T` | [types.md](types.md) |
| A C pointer | `T*` | [types.md](types.md) |
| A slice | `Span<T>`, `ReadOnlySpan<T>` | [types.md](types.md) |
| A fixed array | `[InlineArray(N)]` struct, `fixed` buffer, `stackalloc` | [types.md](types.md) |
| Text | `ReadOnlySpan<byte>` holding UTF-8 | [types.md](types.md) |
| An optional value | `T?` | [types.md](types.md) |
| A fallible result | `Result`, `Result<T>`, `Result<T, E>` | [errors.md](errors.md) |
| An error set | an `enum` | [errors.md](errors.md) |
| Propagate an error | `expr.Try()` | [errors.md](errors.md) |
| Cleanup at scope exit | `Defer`, `ErrDefer`, `using` | [memory.md](memory.md) |
| A tagged union | a `[Union]` interface and its implementing structs | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| Static polymorphism | a generic constrained by an interface | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| Dynamic dispatch | an interface value made by `Dyn<I>.Of` | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| A callback with context | a delegate (function plus context pointer) | [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| A bare C function pointer | `delegate*<...>` | [c-interop.md](c-interop.md) |
| Compile-time evaluation | `static readonly` initializers, `Comptime(...)`, `[Comptime]` parameters | [comptime.md](comptime.md) |
| Compile-time type parameters | generics | [comptime.md](comptime.md) |
| Unrecoverable failure | `throw Panic("...")`, `throw Unreachable()` | [errors.md](errors.md) |
| Uninitialized memory | `Unsafe.SkipInit(out x)` | [memory.md](memory.md) |
| Call C | `[Import]` a header, or `[DllImport]` an `extern` function | [c-interop.md](c-interop.md) |
| Be called by C | `[Export]` | [c-interop.md](c-interop.md) |
| Unit tests | `[Test]` functions, run by `cdc test` | [modules-build.md](modules-build.md) |

## Errors and optionals

```csharp
enum ParseError { Empty, InvalidDigit }

static Result<uint, ParseError> ParseDecimal(ReadOnlySpan<byte> text)
{
    if (text.Length == 0) return ParseError.Empty;
    uint value = 0;
    foreach (var digit in text)
    {
        if (digit < '0' || digit > '9') return ParseError.InvalidDigit;
        value = value * 10 + (uint)(digit - '0');
    }
    return value;
}

static uint PortOrDefault(ReadOnlySpan<byte> text) => ParseDecimal(text).Catch(8080u);
```

`value * 10` overflowing is illegal behavior: a panic in safe builds. `unchecked(value * 10)`
wraps; `checked(value * 10)` always panics. See [types.md](types.md).

## Generics are compile-time parameters

```csharp
static T Max<T>(ReadOnlySpan<T> values) where T : unmanaged, IComparisonOperators<T, T, bool>
{
    var best = values[0];
    foreach (var value in values)
        if (value > best) best = value;
    return best;
}
```

Each use with a different `T` compiles a separate copy, with `>` resolved to the primitive
comparison of that `T`.

## Comptime

```csharp
static class Tables
{
    public static readonly uint Fib30 = Fib(30);

    static uint Fib(uint n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);
}
```

`Fib30` is computed by the compiler and stored as a constant. There is no static initialization
at run time.

## Interop

```csharp
[Import("sqlite3.h")]
static unsafe partial class Sqlite { }

static class Store
{
    [Export]
    public static int store_version() => 3;
}
```

`cdc` reads `sqlite3.h` with libclang, exposes its declarations under `Sqlite`, includes the real
header in the generated C, and emits `store.h` declaring `int store_version(void);`.
