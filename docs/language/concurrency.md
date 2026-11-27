# Concurrency

Concurrency is a library (D152). Code that waits takes an `IIo`, passed like an `IAllocator`, and
the `IIo` it is given decides what waiting means: holding the thread, or suspending only the task
so the thread runs another. The same function serves every case, so there is no `async` variant of
anything and no function is colored by what it calls.

```csharp
static Result<string> Load(IIo io, IAllocator a, string url)
{
    var response = Try(Http.Get(io, url));
    return response.ReadAll(io, a);
}
```

## Backends

| `IIo` | Tasks run | Waiting | Where |
|---|---|---|---|
| `BlockingIo` | one after another, inside `Task.Run` | holds the thread | every target |
| `ThreadedIo` | in parallel on a `ThreadPool` | holds the waiting thread | every target with threads |
| `EventedIo` | as coroutines on one or more threads | suspends the coroutine; the thread runs another | targets with an address space to reserve, not `wasm32-wasi-musl` |

The program chooses the backend once, usually in `Main`, and passes it down. A test passes
`BlockingIo` and gets a deterministic order.

## Tasks

```csharp
using var a = Task.Run(io, () => Load(io, alloc, urlA));
using var b = Task.Run(io, () => Load(io, alloc, urlB));
Try(Task.WhenAll(io, a, b));
var pages = (Try(a.Wait(io)), Try(b.Wait(io)));
```

- `Task.Run(io, work)` starts `work`, a `Func<T>` or a `Func<Result<T>>`, and returns a
  `Task<T>`. A backend without concurrency may run it to completion in place. `Task.RunConcurrent`
  requires it to run at the same time as its caller and returns `error.NoConcurrency` when the
  backend cannot, which is what a producer and its consumer need to avoid waiting on each other.
- `Task<T>` is a handle to a slot the `IIo` takes from its allocator at spawn, the only allocation
  of a task; the result lives in the slot. The handle holds `work`, so a `Task<T>` made from a
  capturing lambda is a value holding that lambda and cannot leave its frame (`CD2002`).
- A local of type `Task<T>` is a `using` declaration (`CD2010`). `Dispose` cancels the task if it
  has not completed and waits for it, so every exit of the scope, an error returned by `Try`
  included, joins the task before its captured variables die. This is what makes capturing safe
  without a collector: no task outlives the block that started it.
- `Wait(io)` waits and gives the task's `Result<T>`. `Task.WhenAll(io, ...)` waits for every task,
  for two to four tasks of any types or a span of tasks of one type, and gives the first error.
  `Task.WhenAny` gives the index of the first task to complete.
- `Task.Spawn(io, state, body)` starts a task that is not waited: `body` is a `static` lambda
  taking `ref TState`, and `state` is copied into the slot, so nothing refers to the caller.

## Cancellation

`CancellationTokenSource` and `CancellationToken` keep their .NET roles. A canceled operation of
the `IIo` returns `error.Canceled`, which `Try` propagates like any error, so cancellation unwinds
through `Defer` and `ErrDefer` with no exception. `Dispose` of a `Task<T>` cancels through the
task's own token.

## Data parallelism

`Parallel.For(io, from, to, body)`, `Parallel.ForEach(io, span, body)` and
`Parallel.Invoke(io, ...)` return only after every iteration has finished, so `body` may capture
locals. They split the work over the backend's workers; on `BlockingIo` they are loops.

## Channels

`Channel<T>` (bounded or unbounded, from an allocator) moves values between tasks: `Send`,
`Receive`, `TrySend`, `TryReceive`, `Close`. `Send` and `Receive` take the `IIo` and wait as it
waits. `Channel.Select(io, ...)` waits on several channels and gives the index of the one ready.
A closed channel returns `error.Closed`.

## Threads and shared memory

Threads, atomics and locks are in [memory.md](memory.md#threads-and-atomics) and the std
([../std.md](../std.md#concurrency)). A lock is held for a scope with `using`:

```csharp
using var scope = mutex.EnterScope();
```

Data races are not detected by the compiler. `Sanitize` set to `Thread` builds a `Debug` program
with the thread sanitizer of the toolchain where it has one.

## Evented stacks

Each coroutine of `EventedIo` runs on its own stack (D152), carved from one reservation of address
space that the backend makes at start: 1 MiB per stack by default on 64-bit targets and 64 KiB on
32-bit ones. Pages become memory only when touched, so a coroutine costs what it uses. Stacks are
reused from a pool, and `Trim` returns the pages a deep call left behind.

| Option of `EventedIo` | Meaning | Default |
|---|---|---|
| `StackSize` | the reservation per stack | 1 MiB, or 64 KiB on 32-bit targets |
| `MaxTasks` | the number of stacks the reservation holds | 65536 |
| `GuardPages` | a page without access below each stack, so an overflow becomes a panic with its message | `true` |
| `Scheduling` | `Stealing`: idle threads take coroutines from busy ones, which balances uneven work; `PerCore`: one thread per core, each keeping its coroutines, memory and queue, which shares nothing and keeps caches warm, for servers whose connections are alike | `Stealing` |

Each guard page splits the reservation into separate mappings, and Linux limits the mappings of a
process (`vm.max_map_count`, 65530 by default), so guarded stacks stop near 30000 tasks unless the
limit is raised, as servers commonly do. Without guard pages the reservation is one mapping and
scales to the address space, and an overflow is no longer detected: it is the option for a
program that needs the scale and accepts that.

On Linux, `EventedIo` submits io_uring operations in batches, one system call for all the
operations ready since the last one, and registers the buffers of its reads and writes with the
kernel, so the data of a socket or a file is not copied between mappings on each operation.

## From C# `async`

| C# | CΔ |
|---|---|
| `async Task<T> LoadAsync(...)` | `Result<T> Load(IIo io, ...)` |
| `await LoadAsync(x)` | `Try(Load(io, x))` |
| `Task.Run(() => ...)` | `using var t = Task.Run(io, () => ...)` |
| `await Task.WhenAll(a, b)` | `Try(Task.WhenAll(io, a, b))` |
| `await t` | `Try(t.Wait(io))` |
| `ConfigureAwait(false)`, `ValueTask`, sync-over-async | not needed: there is one function, not two |
| `IAsyncEnumerable<T>`, `await foreach` | a `Channel<T>`, or an iterator over values already read |
| `CancellationToken` | the same, with `error.Canceled` instead of an exception |
| `lock (o) { }` | `using var scope = mutex.EnterScope();` |
