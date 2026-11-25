# C# frontend

The C# frontend turns CΔ sources into `Lowered` Delta IR. Roslyn does parsing, binding, type
checking, overload resolution, definite assignment, ref safety, flow analysis and control flow
lowering. The frontend adds the subset rules and the translation to IR.

## Roslyn compilation

One `CSharpCompilation` per package, created by the driver in dependency order.

| Setting | Value |
|---|---|
| `OutputKind` | `DynamicallyLinkedLibrary` always: the entry point is a CΔ concept, so Roslyn's own entry point rules (`CS5001`) never apply |
| `LanguageVersion` | the pinned C# version (14) |
| `AllowUnsafe` | `true` |
| `CheckOverflow` | `false`: explicit `checked` and `unchecked` are read from syntax, see below |
| `NullableContextOptions` | `Disable` |
| `Deterministic` | `true` |
| Preprocessor symbols | the build symbols of [../language/modules-build.md](../language/modules-build.md#conditional-compilation) |
| References | the corelib, and `ToMetadataReference()` of each dependency's compilation; never the BCL |
| Specific diagnostic options | the promotions and suppressions of [../language/subset.md](../language/subset.md) |
| Syntax trees | the package's sources, the generated import stubs, and the SDK's global usings file |

The global usings file contains:

```csharp
global using System;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using CDelta;
global using static CDelta.Intrinsics;
```

## The corelib

`lib/corelib` is C# compiled by Roslyn with no standard library, producing a reference assembly.
It declares only what Roslyn requires or what CΔ exposes, and every body is empty or `throw
null`; nothing in it is ever executed.

| Group | Contents |
|---|---|
| Required by Roslyn | `Object`, `ValueType`, `Enum`, `Void`, `Boolean`, `Char`, the integer and float types, `IntPtr`, `UIntPtr`, `String`, `Array`, `Attribute`, `AttributeUsageAttribute`, `AttributeTargets`, `Delegate`, `MulticastDelegate`, `Exception`, `Type`, `RuntimeTypeHandle`, `RuntimeFieldHandle`, `RuntimeMethodHandle`, `Nullable<T>`, `ParamArrayAttribute`, `FlagsAttribute`, `IDisposable`, `ValueTuple<...>` |
| Spans and ranges | `Span<T>`, `ReadOnlySpan<T>` (ordinary structs, with intrinsic `Length`, indexers, slicing, enumerators), `Index`, `Range` and its enumerator extension |
| Compiler services | `InlineArrayAttribute`, `IsExternalInit`, `InterpolatedStringHandlerAttribute`, `InterpolatedStringHandlerArgumentAttribute`, `MethodImplAttribute`, `SkipLocalsInitAttribute`, `RuntimeHelpers` members collection expressions need, `Unsafe`, `CallConv*` types, `UnmanagedCallersOnlyAttribute`, `IsUnmanagedAttribute`, `ScopedRefAttribute`, `RefSafetyRulesAttribute` |
| Interop | `StructLayoutAttribute`, `LayoutKind`, `FieldOffsetAttribute`, `DllImportAttribute`, `CallingConvention`, `CLong`, `CULong`, `NFloat`, `MemoryMarshal` subset |
| Numerics | `INumber<T>`, `IBinaryInteger<T>`, `IFloatingPoint<T>`, the operator interfaces, `Int128`, `UInt128`, `Half`, `Math`, `MathF` subset |
| Threading | `Interlocked`, `Volatile`, `ThreadStaticAttribute` |
| Generic delegates | `Action<...>`, `Func<...>` up to 16 parameters |
| CΔ | everything in [../language/intrinsics-attributes.md](../language/intrinsics-attributes.md) |

### Binding support

Some members exist only because Roslyn requires them to bind a construct the subset keeps. They
carry the exact BCL signature, are never called by lowering, and are listed here:

| Construct | Members |
|---|---|
| `foreach` over a static array | `System.Collections.IEnumerable`, `System.Collections.IEnumerator` |
| extension methods | `System.Runtime.CompilerServices.ExtensionAttribute` |
| inline array element access | `Unsafe.As<TFrom, TTo>(ref TFrom)`, `Unsafe.Add<T>(ref T, int)`, `MemoryMarshal.CreateSpan<T>(ref T, int)`, `MemoryMarshal.CreateReadOnlySpan<T>(ref readonly T, int)` |
| `fixed` over a span | `Span<T>.GetPinnableReference()`, `ReadOnlySpan<T>.GetPinnableReference()` |
| a static array converted to a span | the implicit conversions `T[]` to `Span<T>` and to `ReadOnlySpan<T>` |
| `x.GetValueOrDefault()` | `Nullable<T>.GetValueOrDefault()`, `Nullable<T>.GetValueOrDefault(T)` |
| `where T : unmanaged` | `System.Runtime.InteropServices.UnmanagedType` |
| `foreach` over a string | `String.GetEnumerator()` returning `System.Text.StringRuneEnumerator` with `Current` and `MoveNext()` |

When Roslyn needs another member to bind a kept construct, adding it with its BCL signature and a
row here is implementation, not design: the same commit adds the member, the row and a probe that
binds the construct. A member that would make Roslyn accept a removed construct is design.

Roslyn decides from fields whether a struct is unmanaged, which decides where `T*` is allowed. The
corelib declares these private fields, never read by the compiler, which takes layouts from the
IR:

| Type | Fields |
|---|---|
| `Span<T>`, `ReadOnlySpan<T>` | `nint _pointer; int _length;` |
| `Nullable<T>` | `T _value; bool _hasValue;` |
| `String` | `nint _pointer; int _length;` (a `readonly struct`) |
| `System.Text.Rune` | `int _value;` |
| `Result` | `ushort _code;` |
| `Result<T>`, `Result<T, E>` | `T _value; ushort _code;` |
| `Error` | `ushort _code;` |
| `Void` | none |
| `Index`, `Range` | as in C#: `int _value;`, and two `Index` fields |
| `Int128`, `UInt128` | `ulong _lower; ulong _upper;` |
| `Half` | `ushort _value;` |
| `CLong`, `CULong`, `NFloat` | `nint _value;`, `nuint _value;`, `nint _value;` |

So a span is unmanaged whatever its element type, and a `Result` is unmanaged exactly when its
payload is.

The frontend recognizes corelib members by their documentation comment ID
(`M:CDelta.Intrinsics.Defer(System.Action)`) in a table built once per compilation, and checks
the corelib's assembly identity against the compiler's (`CD0008`).

## Subset checker

`CDelta.Frontend.CSharp.Rules` targets `netstandard2.0` so that it loads in every IDE. It contains:

- `SubsetAnalyzer`, a `DiagnosticAnalyzer` that registers syntax node actions for the syntactic
  rules and operation actions for the semantic ones;
- `SubsetSuppressor`, a `DiagnosticSuppressor` for the site-specific suppressions (`CS8509` on a
  `[Union]` switch that CΔ proves exhaustive);
- the allowlists of `SyntaxKind` and `OperationKind`; anything outside them is `CD1000`.

The compiler runs the same analyzer and suppressor through `CompilationWithAnalyzers`, so the
diagnostics of `cdc` and of the IDE are the same by construction. Every `CD1xxx`, `CD2xxx`,
`CD3xxx` and `CD5xxx` code is produced here, except those that need instantiation (`CD5005`) or
comptime; those come from the core. Each rule is a small class with its code, its registration and
its tests.

## Reachability

The frontend does not lower everything. The core starts from the roots (`Main`, `[Export]`,
`[Test]` in test builds, `[PanicHandler]`, static initializers of reachable globals) and asks for
bodies through `IBodyProvider`. The subset checker still covers all code, so an unused function
with a violation is still an error, which keeps the IDE and the build consistent.

- Before the core runs, the frontend declares the root functions and marks them with the
  attributes `cdelta.EntryPoint` (the `Main` of an executable), `cdelta.Export`, `cdelta.Test` and
  `cdelta.PanicHandler`. The core's roots pass collects the functions that carry them.
- Every other type, function and global is declared the first time a lowered body or a declared
  signature refers to it. Its body is lowered only when the core asks.
- The frontend keeps the map from each `FunctionId` it declared to its Roslyn symbol.

## Symbols to IR

| Roslyn | IR |
|---|---|
| namespace and static class | path segments of member names; no IR type |
| struct | `Struct`; fixed buffers become `array` fields; `[InlineArray(n)]` becomes a struct with one `array<T, n>` field |
| enum | `Enum`, exhaustive unless `[Flags]`, `[NonExhaustive]` or imported |
| `[Union]` interface and its implementers | `Union` with the implementers in declaration order, then by file path |
| other interface | `Interface`, and one `Impl` per implementing struct |
| delegate type | `closure(sig)`; the delegate's name is kept as an attribute |
| function pointer type | `fn(sig)` |
| tuple | a canonical anonymous struct per element type list |
| `Nullable<T>` | `optional<T>` |
| `Result`, `Result<T>`, `Result<T, E>` | `result<void, anyerror>`, `result<T, anyerror>`, `result<T, errorset(E)>` |
| `Error` | `anyerror` |
| `Span<T>`, `ReadOnlySpan<T>` | `slice<T>`, `slice<T> const` |
| `string` | `slice<u8> const text` |
| `T*`, `ref T`, `in T` | `ptr<T>` with the matching flags; a `ref`, `in` or `out` parameter also has its mode |
| method | `Function`; instance methods get `this` as the first parameter ([ir.md](ir.md#functions)) |
| static field | `Global` |
| attribute application | `Attribute` in the `cs.` namespace, or the owning namespace for CΔ attributes |

IR names are the fully qualified C# names. Keys ([ir.md](ir.md#module)) are documentation
comment IDs without the kind prefix; constructs without one get keys derived from their method:

| Construct | Key |
|---|---|
| lambda or anonymous method | `<method key>/lambda<n>`, `n` counting from `0` in source order within the method |
| local function | `<method key>/<name>` |
| closure frame struct | `<method key>/frame` |
| `Comptime(...)` region | `<method key>/comptime<n>` |
| static initializer of a global | `<global key>/init` |
| tuple struct | `tuple<T1,T2,...>` with the element types in the text format |

## Lowering bodies

For each requested method the frontend takes `IMethodBodyOperation` from the semantic model and
builds Roslyn's `ControlFlowGraph`. Roslyn has already lowered short-circuit operators, `?:`,
`??`, `?.`, loops, `switch` statements and expressions, `using` and `foreach` into blocks and
branches, with explicit flow captures for evaluation order. The translation is then:

| Roslyn CFG | IR |
|---|---|
| `BasicBlock` | `Block` |
| `ConditionalSuccessor`, `FallThroughSuccessor` | `branch`, `goto` |
| `IFlowCaptureOperation`, `IFlowCaptureReferenceOperation` | a temp local |
| `ControlFlowRegion` of kind `LocalLifetime` | `live` and `dead` |
| `TryAndFinally` region (only from `using`) | the finally body copied on each exit edge, like `Defer` |
| invocations, operators, conversions, member references | the corresponding rvalues and statements |
| an operator or conversion whose `OperatorMethod` is a user method | `call direct` of that method; Roslyn has already chosen the `checked` form in a checked context |
| a lifted operator (`IsLifted`) on optionals | expanded as C# defines it: arithmetic gives `optional.none` unless both operands have a payload; `==` is true when both have none and compares payloads when both have one; `< > <= >=` are false unless both have a payload |
| `IIsPatternOperation` | pattern lowering, below |
| enumerator calls of corelib spans and ranges | an index loop |
| `IFlowAnonymousFunctionOperation` | closure conversion, below |

### Checked context

Each arithmetic and conversion operation looks at its syntax ancestors: the nearest
`checked(...)`, `checked { }`, `unchecked(...)` or `unchecked { }` gives the mode `checked` or
`wrap`; none gives `default`.

### `Defer`, `ErrDefer`, `using`

1. Each `Defer` or `ErrDefer` invocation is recorded with its block, its enclosing local lifetime
   region, and the CFG of its lambda (`GetAnonymousFunctionControlFlowGraph`).
2. For every branch that leaves regions (`ControlFlowBranch.LeavingRegions`), the active deferred
   bodies are those in the left regions whose invocation block dominates the branch source.
   Dominators are computed on the Roslyn CFG.
3. The bodies are inlined on that edge in reverse order of their invocations. `ErrDefer` bodies are
   placed under a test of the returned value when the edge ends in a `return`, and unconditionally
   on the error edge of a `Try`.

Because the bodies are inlined, captured variables are the enclosing locals themselves, which is
exactly the semantics of [../language/memory.md](../language/memory.md#defer).

### `Try`

`Try(r)` splits its block: evaluate `r` into a temp, `branch haspayload`. The success edge reads
the payload. The error edge reads `err`, casts it with `error.error` to the error set of the
method's return type, runs every active deferred body up to the method's outermost region, then
returns `result.err` of the method's return type. Whether the error sets are compatible was
already checked by `CD3002`. The other result forms are in [ir.md](ir.md#places-operands-and-values).

### Error return trace

- `trace.push` is emitted on every `Try` error edge, after the deferred bodies and before the
  `return`, and before every `return` whose value is built by `result.err` or by an implicit
  conversion from an error.
- `Catch` and `TryGet` save the trace length into a temp (`trace.save`) before evaluating their
  operand, and restore it (`trace.restore`) on the edge where the error is handled, so a handled
  error leaves no entries.

### Patterns

Each pattern lowers to a sequence of tests and binds:

| Pattern | Lowering |
|---|---|
| constant, relational | comparison |
| `and`, `or`, `not` | branches |
| declaration, `var` | assignment to the declared local |
| type on a `[Union]` value | `tag` comparison, then a copy of `payload(member)` |
| type on a type parameter | folded during instantiation |
| property (on fields), positional (tuples) | recursion on each field |
| list (on spans) | `len` test, then element patterns |
| discard | nothing |

No decision tree is built: the C compiler optimizes chains of comparisons.

### Lambdas and closures

- `DataFlowAnalysis` gives the captured variables of each lambda and local function.
- The captured locals of a method move into one frame struct local, declared at method entry;
  every access to them becomes a field access of the frame.
- Each lambda becomes an IR function whose first parameter is a pointer to the frame. Its delegate
  value is `closure.make(function, addr(frame))`.
- A lambda that captures nothing becomes a function with an ignored context parameter, and its
  delegate has a null context; converted to a function pointer it is used directly.
- `CD2002` looks at the operation that consumes a capturing delegate: `return`, assignment to a
  field, a static, or a `ref` or `out` parameter.

### Strings

A `string` constant becomes a `bytes` constant: the UTF-8 encoding of the C# string, with a NUL
after it, typed `slice<u8> const text`, or `ptr<u8> const nullable many` when converted to
`byte*`. Equal literals share one constant. `Length`, the indexers and the range indexer of
`String` lower as those of `ReadOnlySpan<byte>`; the other forms are in
[ir.md](ir.md#places-operands-and-values).

### Interpolated strings

Roslyn already lowers an interpolated string converted to a handler into a handler creation and
calls to `AppendLiteral` and `AppendFormatted`; the frontend lowers those calls like any other,
with literal parts as `bytes` constants.

### Comptime

| Source | IR |
|---|---|
| `Comptime(() => expr)` | a synthetic function returning `expr`, used through `comptime(function)` |
| `Comptime(() => { ... })` | a synthetic function returning `void`, used through `comptime(function)` in a statement |
| `[Comptime]` parameter | a comptime parameter of the function |
| static field initializer | the global's comptime initializer function |
| `Builtin.*`, `TypeInfo.Of<T>()`, `typeof`, `Embed`, `CompileError` | intrinsics evaluated by the core |

## Spans

Every IR statement gets the Roslyn location of the operation it came from: file, line and column
of the start. Generated import stubs map back to the header location recorded by the C frontend.
