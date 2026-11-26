# Errors

CΔ has no exceptions. A function that can fail returns an error union, and the caller must handle
it. A situation that cannot be handled is a panic, which stops the program.

## Error sets

An error set is an ordinary `enum`:

```csharp
enum FileError { NotFound, AccessDenied, IsDirectory }
enum ConfigError { NotFound, Malformed }
```

The identity of an error is its **member name**, across the whole program: `FileError.NotFound`
and `ConfigError.NotFound` are the same error. The compiler gives every distinct error name of the
program one code, starting at `1`; `0` means success. The numeric values declared in the enum
remain the values of the enum itself and are unrelated to error codes.

`Error` (corelib) is the set of all errors of the program. `error.Name` is its member name as
UTF-8 text, from a table the compiler emits only if `Name` is used.

## Error unions

| Type | Meaning | Layout |
|---|---|---|
| `Result` | success or any error | `uint16_t` code |
| `Result<T>` | a `T` or any error | `T` and a `uint16_t` code |
| `Result<T, E>` | a `T` or an error of set `E` | `T` and a `uint16_t` code |
| `Result<Void, E>` | success or an error of set `E` | `uint16_t` code |

Codes are `uint16_t` until a program has more than 65535 distinct error names, which is an error.

Making them:

```csharp
static Result<int, ParseError> Digit(byte c)
{
    if (c < '0' || c > '9') return ParseError.InvalidDigit;
    return c - '0';
}

static Result<File> Open(ReadOnlySpan<byte> path)
{
    if (path.Length == 0) return Fail(FileError.NotFound);
    ...
}
```

- A `T` converts implicitly to `Result<T>` and `Result<T, E>`.
- An `E` converts implicitly to `Result<T, E>`.
- `Fail(e)` converts any error set member to `Error`, which converts implicitly to `Result` and
  `Result<T>`. A generic conversion from every enum cannot be declared in C#, hence `Fail`.
- `default` is success with the default payload: `return default;` ends a `Result` function.

## Handling

| Need | CΔ |
|---|---|
| Propagate to the caller | `var f = Try(Open(path));` |
| Replace an error with a value | `var n = Catch(Parse(s), 0);` |
| Inspect | `if (!r.TryGet(out var value, out var error)) { ... }` |
| Branch on the error | `switch (error) { case FileError.NotFound: ... }` |
| Assert success | `r.Value`: illegal behavior if `r` is an error |
| Read the error | `r.Error`: illegal behavior if `r` is a success |
| Test | `r.IsError` |
| Ignore deliberately | `_ = Close(f);` |

### `Try`

`Try(r)` evaluates `r`; on success it yields the payload, on error it returns the error from the
enclosing function, running the pending `ErrDefer` and `Defer` bodies first.

- The enclosing function must return a `Result` type (`CD3001`).
- If it returns `Result<U, F>`, every member name of the operand's set must exist in `F`
  (`CD3002`). If it returns `Result` or `Result<U>`, any set is accepted.
- `Try` is a function call in syntax and a control flow statement in meaning. It is the only
  intrinsic that returns from its caller; its name says so.

### A `Result` cannot be dropped

An expression statement whose value is a `Result` type is an error (`CD3004`). Discard explicitly
with `_ =`.

## `ErrDefer`

```csharp
static Result<Buffer> Make(IAllocator a)
{
    var memory = Try(a.Alloc(4096, 16));
    ErrDefer(() => a.Free(memory));
    var header = Try(WriteHeader(memory));
    return new Buffer(memory, header);
}
```

`ErrDefer(() => body)` runs `body` when the scope exits by returning an error, through `Try` or
through a `return` whose value is an error. It is allowed only in functions returning a `Result`
(`CD3003`). The argument of `Defer` and `ErrDefer` must be a lambda written in place (`CD3005`);
it is inlined, never turned into a delegate. Ordering with `Defer` is in
[memory.md](memory.md#defer).

## Error return traces

In Debug and ReleaseSafe the compiler records, per thread, the source location of each `Try` and
`return` that propagated an error, in a fixed ring of 32 entries. When `Main` returns an error or a
panic happens while an error is in flight, the trace is printed. ReleaseFast and ReleaseSmall
record nothing. The ring needs no allocation and is part of the generated code, not a runtime.
An error handled by `Catch` or `TryGet` removes the entries it added, so the trace shows only the
path of the error that is still in flight. The output format is in
[../compiler/backend-c.md](../compiler/backend-c.md#output-of-a-panic).

## Panics

```csharp
throw Panic("index table corrupted");
throw Unreachable();
```

- A panic calls the panic handler and never returns. Nothing unwinds and no `Defer` runs.
- `Panic` and `Unreachable` are intrinsics returning a corelib `Exception`. They can only be the
  operand of `throw` (`CD3007`), and `throw` accepts nothing else (`CD1011`). Using `throw` gives
  them C# flow analysis for free: code after them is unreachable, and a function ending in one
  needs no `return`.
- `Unreachable` in Debug and ReleaseSafe panics with "reached unreachable code". In ReleaseFast
  and ReleaseSmall reaching it is illegal behavior, so the C compiler may optimize on it.
- Every safety check failure ([memory.md](memory.md#build-modes-and-illegal-behavior)) is a panic.

### The panic handler

The default handler writes the message, the source location and the error return trace to
standard error when libc is linked, then aborts; without libc it executes a trap instruction. A
program replaces it with one function:

```csharp
[PanicHandler]
static void OnPanic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> file, int line)
{
    ...
    throw Unreachable();
}
```

Only one `[PanicHandler]` may exist in a program (`CD3008`), and it must not return.

## `Main`

| Signature | Exit code |
|---|---|
| `static void Main()` | `0` |
| `static int Main()` | the returned value |
| `static Result Main()` | `0` on success; on error `1`, with `error: <Name>` and the trace on standard error |
| `static Result<int> Main()` | the returned value, or as above |

Any other `Main` is `CD3006`. Arguments are read with `Std.Process.Args()`.

Top-level statements are the entry point of their file, returning nothing or an `int` as in C#.
They cannot use `Try`, since they do not return a `Result`; `Std.Process.Run` runs a function
returning `Result` or `Result<int>`, reports an error as a failing `Main` would, and gives the
exit code:

```csharp
using Std;

return Process.Run(Run);

static Result Run()
{
    var lines = Try(CountLines(Process.Args()[1]));
    Debug.Print($"{lines}\n");
    return default;
}
```
