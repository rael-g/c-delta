# Memory

## Where memory comes from

| Storage | How | Lifetime |
|---|---|---|
| Static | `static` fields, `static readonly` arrays, literals | the whole program |
| Thread-local | `[ThreadStatic] static` fields | the thread |
| Stack | locals, `stackalloc`, collection expressions targeting spans | the enclosing function |
| Allocator | an `IAllocator` passed explicitly | until freed through the same allocator |

There is no other source. The language never allocates on its own: no construct, intrinsic or
conversion allocates, and a function of the std that allocates takes an `IAllocator` parameter.

## Allocators

`IAllocator` is the std interface every allocator implements:

```csharp
public interface IAllocator
{
    Result<Span<byte>> Alloc(nuint size, nuint alignment);
    Result<Span<byte>> Resize(Span<byte> memory, nuint size, nuint alignment);
    void Free(Span<byte> memory);
}
```

The std provides generic helpers on top (`Create<T>`, `Destroy<T>`, `AllocSpan<T>`, `FreeSpan<T>`)
and the allocators listed in [std.md](../std.md). An allocator is passed as an interface value
(`Dyn<IAllocator>.Of(ref arena)`), so allocating code is not generic over it; code that wants
static dispatch takes a type parameter `TAlloc : IAllocator` instead.

Allocation failure is a `Result` error (`Std.Mem.AllocError.OutOfMemory`), never a panic.

## Defer

```csharp
var file = Try(Fs.Open(path));
Defer(() => file.Close());
```

- `Defer(() => body)` schedules `body` to run when the enclosing block exits: falling off its end,
  `break`, `continue`, `goto` out of it, `return`, or `Try` returning an error.
- Bodies run in reverse order of their `Defer` calls in that block, and a block's bodies run before
  those of the enclosing block.
- `ErrDefer` bodies take part in the same order and run only on an error exit
  ([errors.md](errors.md#errdefer)).
- A body cannot leave itself with `return`, `break`, `continue` or `goto`; C# lambda rules already
  forbid it. A body may not call `Try` (`CD3009`).
- A body sees variables as they are when it runs, not when `Defer` was called, which is what both
  the C# capture rules and deferred execution mean.
- A panic runs no body.

### `using`

`using var x = expr;` and `using (expr) { }` call `Dispose` at scope exit and are exactly
`Defer(() => x.Dispose())` placed after the declaration. They are kept because they are C# and add
no behavior. `Dispose` is called on the variable, not on a copy.

## Lifetimes

CΔ does not track lifetimes beyond what Roslyn already does for `ref`, `ref struct` and `scoped`,
plus two checks of obvious mistakes:

- returning `stackalloc` memory or the address of a local (`CD2006`);
- a capturing lambda escaping its frame (`CD2002`,
  [generics-interfaces-delegates.md](generics-interfaces-delegates.md#lambdas-and-closures)).

Everything else (use after free, double free, dangling pointers into a freed buffer) is the
programmer's responsibility. CΔ promises no full memory safety; it offers help in three opt-in
layers (D146):

- **Checked views.** `ref struct` types of the std, such as a scoped span, get Roslyn's ref
  safety analysis: the compiler proves they never outlive the memory they view. Plain spans stay
  unchecked ordinary structs.
- **Lints.** An optional analyzer package warns about likely mistakes inside a function: a use
  after `Free`, an allocation with no `Free` or `Defer` on some exit path, stack memory stored in
  a field or global. It is a heuristic, not a proof.
- **Run time.** `Std.Heap.DebugAllocator` detects leaks, double frees and use after free in Debug
  builds by never reusing addresses and poisoning freed memory; `Pool<T>` turns a use after free
  of a handle into an error value.

## Uninitialized memory

C# requires definite assignment and zeroes `stackalloc`, `new` and `default`. To skip it:

| Need | CΔ |
|---|---|
| An uninitialized local | `Unsafe.SkipInit(out T x);` |
| Uninitialized `stackalloc` in a method | `[SkipLocalsInit]` on the method, struct or module |

Reading uninitialized memory is illegal behavior. In Debug, uninitialized memory is filled with
`0xAA` bytes so that reads show up.

## Build modes and illegal behavior

| Mode | Optimization | Safety checks | Debug info |
|---|---|---|---|
| `Debug` | none | all | full |
| `ReleaseSafe` | speed | all | full |
| `ReleaseFast` | speed | none | none by default |
| `ReleaseSmall` | size | none | none by default |

Each kind of illegal behavior, and how safe modes detect it:

| Illegal behavior | Check in safe modes |
|---|---|
| Integer overflow in the default context | panic "integer overflow"; with checks off it wraps, as C# without overflow checking, and is not illegal |
| Integer conversion out of range in the default context | panic "integer cast truncated bits"; with checks off it truncates |
| Float to integer out of range or `NaN` in the default context | panic "float cast out of range"; with checks off it saturates, `NaN` to `0` |
| Division or remainder by zero, `MinValue / -1` | panic "division by zero" or "integer overflow" (every mode, `checked` or not) |
| Span or inline array index out of bounds, invalid slice | panic "index out of bounds" |
| Dereference of a null pointer | panic "null pointer dereference" |
| Call through a null delegate, function pointer or interface value | panic "null call" |
| `Nullable<T>.Value` on `null` | panic "attempt to use null value" |
| `Result.Value` on an error, `Result.Error` on a success | panic "attempt to unwrap error" or "attempt to read error of success" |
| A switch expression with no matching arm | panic "no switch arm matched" (every mode, as C# throws) |
| Reaching `Unreachable()` | panic "reached unreachable code" |
| Misaligned pointer cast | panic "incorrect alignment" |
| Overlapping `Std.Mem.Copy` | panic "overlapping copy" |
| Reading uninitialized memory | not detected; `0xAA` fill in Debug |
| Use after free, double free | not detected by the language; detected by `DebugAllocator` |
| Data race | not detected |

In ReleaseFast and ReleaseSmall the compiler and the C compiler assume the others never happen.
Each of them is a place where C# would throw, so a C# program correct in C# stays correct.

A function or block can override the mode's checks:

| Attribute | Effect |
|---|---|
| `[Safety(true)]` | all checks on, in every mode |
| `[Safety(false)]` | all checks off, in every mode |

## Threads and atomics

Threads come from the std (`Std.Thread`, over C11 threads or the OS API). Shared memory follows
the C11 memory model.

- `volatile` fields keep their C# meaning and map to C `volatile`.
- `Interlocked` and `Volatile` (corelib, the C# names) map to C11 `<stdatomic.h>` operations with
  sequentially consistent ordering. `Std.Atomic<T>` adds explicit orderings.
- `[ThreadStatic]` maps to `_Thread_local`.
