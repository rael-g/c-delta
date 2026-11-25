# The C# subset

CΔ is C# 14 with features removed or restricted. This document lists every feature of the
language. A removed or restricted feature has the diagnostic code that rejects it; the codes are
registered in [../compiler/diagnostics.md](../compiler/diagnostics.md).

## How the subset is enforced

- The source must compile under Roslyn with `LangVersion` 14, unsafe code allowed, nullable
  reference types disabled, against the CΔ corelib only. Any Roslyn error is an error.
- The subset checker walks every syntax tree and every operation tree. It is an **allowlist**:
  syntax kinds and operation kinds it does not know are rejected with `CD1000`. A new C# version
  therefore adds nothing to CΔ until each new construct is reviewed and added here.
- The same checker runs as a Roslyn analyzer in the IDE, so violations appear while typing.
- Some Roslyn warnings are errors in CΔ; see the table at the end.

## The rule behind the cuts

A construct is removed when it requires a garbage collector or a runtime (classes, arrays,
`object`, exceptions, `async`), when it makes syntax call user code invisibly (properties,
indexers, implicit conversions, finalizers, events), or when it duplicates a kept construct with
hidden behavior. Operators and explicit conversions on user types are kept: the operand's type
shows that the type's code runs, and an operator receives nothing but its operands (D143).

The corelib is exempt from the hidden-call rule: it declares properties, indexers and implicit
conversions (`Span<T>.Length`, `Span<T>[int]`, the conversion of a `string` literal), because they
are intrinsics that the compiler implements directly and Roslyn needs them declared. User code,
including the std, cannot declare them.

## Lexical structure

| Feature | Status |
|---|---|
| Identifiers, keywords, comments, literals | Kept |
| Integer, real, character and boolean literals | Kept |
| Regular, verbatim and raw string literals | Kept; meaning in [types.md](types.md#text) |
| UTF-8 literals (`"x"u8`) | Kept |
| Interpolated strings | Restricted: only when converted to an interpolated string handler type. `CD1021` |
| `#if`, `#elif`, `#else`, `#endif` | Restricted: only on symbols defined by the build. `CD1023` |
| `#define`, `#undef` | Removed. `CD1023` |
| `#region`, `#pragma warning`, `#nullable`, `#line`, `#error`, `#warning` | Kept |

## Namespaces and compilation units

| Feature | Status |
|---|---|
| Namespaces, file-scoped namespaces | Kept |
| `using` directives, `using static`, aliases, `global using` | Kept |
| `extern alias` | Removed. `CD1000` |
| Top-level statements | Removed. `CD1022` |
| `file` types | Kept: visible only in their file |

## Types

| Feature | Status |
|---|---|
| `struct`, `readonly struct`, `ref struct`, `record struct` | `struct`, `readonly struct`, `ref struct` kept; `record struct` removed. `CD1002` |
| `static class` | Kept: a module of functions and globals, never instantiated |
| Other `class` declarations | Removed. `CD1001` |
| Classes deriving from `System.Attribute` | Kept: they exist only as compile-time metadata |
| `record`, `record class` | Removed. `CD1002` |
| `interface` | Kept; see [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| `enum`, `[Flags]` | Kept |
| `delegate` declarations | Kept; delegates are values, see [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| Nested types, partial types | Kept |
| Generic types and methods | Kept; monomorphized |
| Variance (`in`, `out` on type parameters) | Removed. `CD5003` |
| `object`, boxing | Removed. `CD1017` |
| `dynamic` | Removed. `CD1016` |
| `string` | Restricted: only constants and literals. `CD1020` |
| Arrays `T[]` | Restricted: only as the type of a `static readonly` field initialized with a constant-length initializer. `CD1019` |
| Array creation `new T[n]` | Removed. `CD1019` |
| Anonymous types | Removed. `CD1018` |
| Tuples `(int, int)` | Kept: anonymous structs |
| Nullable value types `T?` | Kept: optionals |
| Nullable reference types | Not applicable: disabled |
| Pointers, function pointers | Kept |
| `TypedReference`, `__makeref`, `__reftype`, `__refvalue` | Removed. `CD1027` |
| `__arglist` | Restricted: only in declarations and calls of variadic C functions. `CD1027` |

## Members

| Feature | Status |
|---|---|
| Fields, `readonly` fields, `const`, `static` fields | Kept |
| Non-public fields | Removed: every field, `static` and `fixed` fields included, is `public`. `const` declarations are constants, not fields, and take any visibility. `CD1025` |
| `volatile` fields | Kept |
| `[ThreadStatic]` static fields | Kept: thread-local storage |
| Methods (static and instance), local functions | Kept |
| Instance methods of structs | Kept: `this` is a reference to the receiver |
| `readonly` members | Kept |
| Extension methods, extension blocks (methods only) | Kept |
| Extension properties | Removed. `CD1004` |
| Extension operators (in extension blocks) | Kept with the same set as operators below; this is how operators reach imported C structs |
| Instance constructors of structs, primary constructors of structs | Kept: `new` never allocates |
| Field initializers in structs | Kept |
| Static constructors | Removed: static state is initialized at comptime. `CD1009` |
| Finalizers | Removed. `CD1008` |
| Properties (including auto, `init`, `required`, partial, `field`) | Removed. `CD1004` |
| Indexers | Removed. `CD1005` |
| Events | Removed. `CD1006` |
| Operators: unary `+ - ! ~`; binary `+ - * / % & \| ^ << >> >>>`; `== != < > <= >=`; their `checked` forms | Kept: a call to the operator method (D143) |
| Explicit conversion operators, and their `checked` forms | Kept: a cast is written at every use |
| Implicit conversion operators; operators `true`, `false`, `++`, `--`; compound assignment operators | Removed. `CD1007` |
| `Deconstruct` methods | Removed: tuples deconstruct natively. `CD1024` |
| `abstract`, `virtual`, `override`, `sealed`, `protected`, `base`, `new` as a modifier | Removed. `CD1003` |
| `public`, `internal`, `private` on types and methods, `file` | Kept: visibility, see [modules-build.md](modules-build.md) |
| `extern` methods | Kept: C functions, see [c-interop.md](c-interop.md) |
| `partial` methods with implementations | Kept |
| `params` with an array | Removed. `CD1028` |
| `params Span<T>`, `params ReadOnlySpan<T>` | Kept |
| `ref`, `out`, `in`, `ref readonly` parameters, `scoped`, `ref` returns, `ref` locals, `ref` fields | Kept |
| Optional parameters, named arguments | Kept |
| Overloading | Kept |
| `required` fields | Kept |

## Statements

| Feature | Status |
|---|---|
| Blocks, declarations, expression statements, `if`, `while`, `do`, `for` | Kept |
| `foreach` | Kept over `Span<T>`, `ReadOnlySpan<T>`, `static readonly` arrays, inline arrays and `Range` (`0..n`) |
| `switch` statements, `goto`, `goto case`, `goto default`, `break`, `continue`, `return` | Kept |
| `switch` over an exhaustive enum without every member or `default` | Error. `CD2001` |
| `checked`, `unchecked` blocks | Kept; see [types.md](types.md#integer-semantics) |
| `unsafe` blocks, `fixed` statements | Kept; `fixed` only takes an address, nothing is pinned |
| `using` statements and declarations | Kept: equivalent to `Defer(() => x.Dispose())` |
| `try`, `catch`, `finally` | Removed. `CD1010` |
| `throw` | Restricted: only `throw Panic(...)` and `throw Unreachable()`. `CD1011` |
| `yield` | Removed. `CD1013` |
| `lock` | Removed. `CD1015` |
| `await`, `async` | Removed. `CD1012` |
| Labeled statements | Kept |

## Expressions

| Feature | Status |
|---|---|
| Arithmetic, bitwise, shift, comparison, logical operators on primitives | Kept |
| Operators on user types | Kept: the declared operator, called as a function |
| `==` on structs | Only when the struct declares it; otherwise `Std.Mem.Eql` |
| Assignment, compound assignment, `??`, `??=`, `?.`, `?:`, `is`, `switch` expressions | Kept |
| Patterns: constant, relational, logical, declaration, `var`, discard, property, positional, list | Kept; property patterns match fields |
| Type patterns | Restricted: only on a `[Union]` interface or a type parameter. `CD5004` |
| `as` | Removed. `CD1026` |
| `new` for structs, target-typed `new()`, object and collection initializers on structs | Kept |
| `with` on structs | Kept |
| Collection expressions | Restricted: only targeting `Span<T>`, `ReadOnlySpan<T>`, inline arrays and `static readonly` arrays |
| Lambdas, anonymous methods, method groups | Kept; see [generics-interfaces-delegates.md](generics-interfaces-delegates.md) |
| Delegate combination (`+`, `-`, `+=`, `-=`) | Removed. `CD1029` |
| `stackalloc` | Restricted: only the pointer form `T* p = stackalloc T[n]`; Roslyn itself rejects the span form (`CS8346`) because spans are not ref structs |
| `sizeof`, `default`, `nameof`, `typeof` | Kept; `typeof` is comptime only, see [comptime.md](comptime.md) |
| `&`, `*`, `->`, pointer arithmetic, pointer comparison | Kept |
| `checked(...)`, `unchecked(...)` | Kept |
| String concatenation | Restricted: only between constants. `CD1021` |
| Query expressions | Removed. `CD1014` |
| `await` | Removed. `CD1012` |
| `throw` expressions | Restricted as `throw` statements. `CD1011` |
| Range and index operators (`..`, `^`) | Kept on spans |
| Null-conditional assignment | Kept |

## Attributes

| Feature | Status |
|---|---|
| Applying attributes, generic attributes, user attribute classes | Kept |
| Array creation and collection expressions as attribute arguments | Kept: they are constants of the attribute, never arrays at run time |
| Reading attributes | At comptime only, see [comptime.md](comptime.md) |

## Roslyn warnings that are errors

| Warning | Meaning |
|---|---|
| CS0168 | Local declared and never used |
| CS0219 | Local assigned and never used |
| CS0162 | Unreachable code |
| CS8321 | Local function declared and never used |
| CS8509 | Switch expression does not handle every value; suppressed at a site where CΔ proves exhaustiveness itself (a `[Union]` value covering every member), and `CD2001` decides instead |
| CS8500 | Address of a managed type |

Discards (`_ = expr;`) are the way to say a value is deliberately unused.

Site-specific suppression uses a Roslyn `DiagnosticSuppressor`, so the IDE and the compiler agree.

## Roslyn warnings that are suppressed

| Warning | Reason |
|---|---|
| CS0626 | An `extern` method without `DllImport` is the normal form of an imported C function |
| CS8524 | Unnamed enum values are handled by `CD2001` according to whether the enum is exhaustive |
| CS0660, CS0661 | A type declaring `==` and `!=` has no `Equals` or `GetHashCode` to override; there is no object model |
