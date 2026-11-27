# Diagnostics

## Format

```
src/Program.cs(12,9): error CD3004: a 'Result' is discarded
src/Program.cs(12,9): note: 'Close' returns 'Result'; handle it or write '_ ='
```

- Path, line and column of the start of the offending code; the MSBuild format, so every C# tool
  links it. The path is the one the driver received: full paths for project items, as typed for
  files on the command line. Line and column are 1-based; the column counts UTF-16 code units.
- Every registered code is an error. CΔ has no warnings of its own: a rule is either a rule or not.
- The message of a code is fixed: exactly the registry text, with each pair of backticks written
  as single quotes. It never contains names or values from the program.
- What is specific to the occurrence (names, the expected set, the other declaration) goes in
  notes, each on its own line with its own location or the diagnostic's. A diagnostic has zero or
  more notes, in a fixed order chosen by the rule.

## Ranges

| Range | Area | Produced by |
|---|---|---|
| `CD0xxx` | driver, project, toolchain | driver |
| `CD1xxx` | removed C# constructs | subset analyzer (IDE and build) |
| `CD2xxx` | types and memory | subset analyzer, except `CD2008` (core) |
| `CD3xxx` | errors and panics | subset analyzer, except `CD3010` (core) |
| `CD4xxx` | comptime | core (build only) |
| `CD5xxx` | generics, interfaces, delegates | subset analyzer, except `CD5005` (core) |
| `CD6xxx` | C interop | C frontend and subset analyzer, except `CD6010` (core) |
| `CD9xxx` | internal compiler errors | driver |

A code is never reused for another meaning. A retired code stays in this table marked retired.

## Registry

| Code | Message | Document |
|---|---|---|
| CD0001 | invalid project, option or package graph (including a dependency cycle) | [driver-toolchain-sdk.md](driver-toolchain-sdk.md) |
| CD0002 | no C toolchain supports the target, or the toolchain is older than the minimum | [driver-toolchain-sdk.md](driver-toolchain-sdk.md#c-toolchain) |
| CD0003 | the C compiler rejected generated C; this is a compiler bug | [driver-toolchain-sdk.md](driver-toolchain-sdk.md#c-toolchain) |
| CD0004 | the C compiler rejected a C source of the project | [driver-toolchain-sdk.md](driver-toolchain-sdk.md#c-toolchain) |
| CD0005 | linking failed | [driver-toolchain-sdk.md](driver-toolchain-sdk.md#c-toolchain) |
| CD0006 | the target is not supported | [../language/modules-build.md](../language/modules-build.md#targets) |
| CD0007 | an executable has no `Main` | [../language/errors.md](../language/errors.md#main) |
| CD0008 | the corelib does not match the compiler | [frontend-csharp.md](frontend-csharp.md#the-corelib) |
| CD0009 | a package uses an edition newer than this compiler; update the compiler | [../language/modules-build.md](../language/modules-build.md#editions) |
| CD0010 | a fetched dependency does not match its hash | [../language/modules-build.md](../language/modules-build.md#fetched-dependencies) |
| CD1000 | this C# construct is not part of CΔ | [../language/subset.md](../language/subset.md) |
| CD1001 | classes are not part of CΔ; use a struct, or a static class for a module | [../language/subset.md](../language/subset.md#types) |
| CD1002 | records are not part of CΔ | [../language/subset.md](../language/subset.md#types) |
| CD1003 | inheritance modifiers and `base` are not part of CΔ | [../language/subset.md](../language/subset.md#members) |
| CD1004 | properties are not part of CΔ, except getter-only static abstract properties and their implementations; use a field or a method | [../language/subset.md](../language/subset.md#members) |
| CD1005 | indexers have only a getter | [../language/subset.md](../language/subset.md#members) |
| CD1006 | events are not part of CΔ | [../language/subset.md](../language/subset.md#members) |
| CD1007 | implicit conversions and the operators `true`, `false`, `++`, `--` and compound assignment are not part of CΔ | [../language/subset.md](../language/subset.md#members) |
| CD1008 | finalizers are not part of CΔ | [../language/subset.md](../language/subset.md#members) |
| CD1009 | static constructors are not part of CΔ; initialize statics with comptime | [../language/subset.md](../language/subset.md#members) |
| CD1010 | `try` is not part of CΔ; return a `Result` | [../language/errors.md](../language/errors.md) |
| CD1011 | `throw` only accepts `Panic(...)` or `Unreachable()` | [../language/errors.md](../language/errors.md#panics) |
| CD1012 | `async` and `await` are not part of CΔ | [../language/subset.md](../language/subset.md#statements) |
| CD1013 | iterators are not part of CΔ | [../language/subset.md](../language/subset.md#statements) |
| CD1014 | query expressions are not part of CΔ | [../language/subset.md](../language/subset.md#expressions) |
| CD1015 | `lock` is not part of CΔ; use `Std.Thread.Mutex` | [../language/subset.md](../language/subset.md#statements) |
| CD1016 | `dynamic` is not part of CΔ | [../language/subset.md](../language/subset.md#types) |
| CD1017 | `object` and boxing are not part of CΔ | [../language/subset.md](../language/subset.md#types) |
| CD1018 | anonymous types are not part of CΔ; use a tuple | [../language/subset.md](../language/subset.md#types) |
| CD1019 | arrays are only allowed as initialized `static readonly` fields | [../language/types.md](../language/types.md#arrays) |
| CD1020 | retired: `string` is a run-time type (D148) | [../language/types.md](../language/types.md#text) |
| CD1021 | only constant strings concatenate, and interpolation needs a handler type | [../language/types.md](../language/types.md#text) |
| CD1022 | retired: top-level statements are kept (D155) | [../language/subset.md](../language/subset.md#namespaces-and-compilation-units) |
| CD1023 | `#define` and `#undef` are not part of CΔ, and `#if` only tests build symbols | [../language/modules-build.md](../language/modules-build.md#conditional-compilation) |
| CD1024 | `Deconstruct` methods are not part of CΔ | [../language/subset.md](../language/subset.md#members) |
| CD1025 | fields must be `public` | [../language/modules-build.md](../language/modules-build.md#visibility) |
| CD1026 | `as` is not part of CΔ | [../language/subset.md](../language/subset.md#expressions) |
| CD1027 | `TypedReference` constructs are not part of CΔ, and `__arglist` is only for variadic C functions | [../language/c-interop.md](../language/c-interop.md#what-a-header-becomes) |
| CD1028 | `params` arrays are not part of CΔ; use `params ReadOnlySpan<T>` | [../language/subset.md](../language/subset.md#members) |
| CD1029 | delegates do not combine | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#delegates) |
| CD1030 | a function pointer with the default calling convention is written `delegate*<...>` | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#function-pointers) |
| CD1031 | `char` only exists as a constant; use `Rune` or `byte` | [../language/types.md](../language/types.md#text) |
| CD1032 | only a constant `string` converts to `byte*`; use `ToCString` | [../language/types.md](../language/types.md#text) |
| CD1033 | the `args` of top-level statements is an array; use `Process.Args()` | [../language/errors.md](../language/errors.md#main) |
| CD1034 | `string` counts no UTF-16 units; positions and counts are bytes in `s.Bytes`, characters are `EnumerateRunes()` | [../language/types.md](../language/types.md#text) |
| CD2001 | this switch does not cover every value; add the missing members or `default` | [../language/types.md](../language/types.md#enums) |
| CD2002 | a capturing lambda cannot outlive the function that created it | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#lambdas-and-closures) |
| CD2003 | an opaque type can only be used behind a pointer | [../language/types.md](../language/types.md#opaque-structs) |
| CD2004 | invalid bit-field | [../language/types.md](../language/types.md#layout) |
| CD2005 | `[ThreadStatic]` applies only to static fields | [../language/memory.md](../language/memory.md#threads-and-atomics) |
| CD2006 | this returns memory of the current stack frame | [../language/memory.md](../language/memory.md#lifetimes) |
| CD2007 | this string literal is not valid UTF-16 and cannot be encoded as UTF-8 | [../language/types.md](../language/types.md#text) |
| CD2008 | a reference or pointer cannot be taken to a field that is not aligned | [../language/types.md](../language/types.md#layout) |
| CD2009 | a lambda that captures a loop variable cannot be stored outside the loop | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#lambdas-and-closures) |
| CD3001 | `Try` needs the function to return a `Result` | [../language/errors.md](../language/errors.md#try) |
| CD3002 | the error set returned here lacks errors `Try` can propagate | [../language/errors.md](../language/errors.md#try) |
| CD3003 | `ErrDefer` needs the function to return a `Result` | [../language/errors.md](../language/errors.md#errdefer) |
| CD3004 | a `Result` is discarded | [../language/errors.md](../language/errors.md#a-result-cannot-be-dropped) |
| CD3005 | `Defer` and `ErrDefer` need a lambda written in place | [../language/errors.md](../language/errors.md#errdefer) |
| CD3006 | invalid `Main` signature | [../language/errors.md](../language/errors.md#main) |
| CD3007 | `Panic` and `Unreachable` must be thrown | [../language/errors.md](../language/errors.md#panics) |
| CD3008 | more than one `[PanicHandler]`, or a handler with the wrong signature | [../language/errors.md](../language/errors.md#the-panic-handler) |
| CD3009 | `Try` cannot be used in a deferred body | [../language/memory.md](../language/memory.md#defer) |
| CD3010 | the program has more than 65535 error names | [../language/errors.md](../language/errors.md#error-unions) |
| CD4001 | comptime code cannot call an extern function | [../language/comptime.md](../language/comptime.md#what-comptime-code-can-do) |
| CD4002 | comptime branch quota exceeded | [../language/comptime.md](../language/comptime.md#what-comptime-code-can-do) |
| CD4003 | this argument must be known at comptime | [../language/comptime.md](../language/comptime.md#comptime-parameters) |
| CD4004 | compile error raised by `CompileError` | [../language/comptime.md](../language/comptime.md#errors-at-comptime) |
| CD4005 | a comptime-only value reaches run time | [../language/comptime.md](../language/comptime.md#comptime-only-values) |
| CD4006 | this static initializer cannot be evaluated at comptime | [../language/comptime.md](../language/comptime.md#what-runs-at-comptime) |
| CD4007 | illegal behavior during comptime | [../language/comptime.md](../language/comptime.md#what-comptime-code-can-do) |
| CD4008 | `Embed` path not found or outside the package | [../language/comptime.md](../language/comptime.md) |
| CD4009 | a comptime result points into a comptime stack frame | [../language/comptime.md](../language/comptime.md#results-of-comptime) |
| CD4010 | this operation is not available at comptime | [../language/comptime.md](../language/comptime.md#what-comptime-code-can-do) |
| CD4011 | the method's signature does not match the delegate it is bound to | [../language/comptime.md](../language/comptime.md#comptime-only-values) |
| CD5001 | a struct converts to an interface only through `Dyn<I>.Of(ref x)`, or to a `[Union]` interface | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#2-as-a-value-dynamic-dispatch) |
| CD5002 | a `[Union]` member must be a non-generic, non-ref struct of the same package | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#3-as-a-tagged-union-union) |
| CD5003 | variance is not part of CΔ | [../language/subset.md](../language/subset.md#types) |
| CD5004 | type patterns only apply to `[Union]` values and type parameters | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#3-as-a-tagged-union-union) |
| CD5005 | generic instantiation nested too deeply | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#generics) |
| CD5006 | `Dyn<I>` needs an interface that is not a `[Union]` | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#2-as-a-value-dynamic-dispatch) |
| CD5007 | a delegate cannot be made from an instance method of a struct; use a lambda | [../language/generics-interfaces-delegates.md](../language/generics-interfaces-delegates.md#delegates) |
| CD6001 | the C header could not be read | [frontend-c.md](frontend-c.md#the-translation-unit) |
| CD6002 | `[Import]` applies only to a `static partial class` | [../language/c-interop.md](../language/c-interop.md#importing-a-header) |
| CD6003 | this C declaration could not be translated | [../language/c-interop.md](../language/c-interop.md#what-a-header-becomes) |
| CD6004 | an exported signature uses a type C cannot represent | [../language/c-interop.md](../language/c-interop.md#exporting) |
| CD6005 | two exports have the same name | [../language/c-interop.md](../language/c-interop.md#exporting) |
| CD6006 | generic and variadic functions cannot be exported | [../language/c-interop.md](../language/c-interop.md#exporting) |
| CD6007 | this `DllImport` option is not supported | [../language/c-interop.md](../language/c-interop.md#declaring-a-c-function-by-hand) |
| CD6008 | `[CMacro]` applies only to `static extern` methods | [../language/c-interop.md](../language/c-interop.md#macros) |
| CD6009 | two imports define the same macro differently | [frontend-c.md](frontend-c.md#the-translation-unit) |
| CD6010 | two `[DllImport]` declarations of one C function have different signatures | [../language/c-interop.md](../language/c-interop.md#declaring-a-c-function-by-hand) |
| CD9001 | internal compiler error | [architecture.md](architecture.md#internal-errors) |
| CD9002 | internal IR verification failure | [ir.md](ir.md#verifier) |

## In the code

`CDelta.Diagnostics` is portable and references no Roslyn assembly. It contains:

- `DiagnosticCode`, an enum with one member per code (`CD3004`), the numeric value being the code's
  number;
- `DiagnosticRegistry`, one static table from code to message;
- `Diagnostic` (code, `Location`, list of `Note`), `Note` (optional `Location`, text) and
  `Location` (path, line, column);
- `DiagnosticBag`, which a stage appends to, and which reports whether it holds an error.

`CDelta.Frontend.CSharp.Rules` builds one Roslyn `DiagnosticDescriptor` per code from the registry
(id `CDnnnn`, category `CDelta`, severity error, title and message format both the fixed message).
An analyzer rule reports notes as additional locations and carries their texts in the diagnostic's
properties under the keys `note0`, `note1`, ...; `cdc` converts Roslyn diagnostics back into
`Diagnostic` with their notes, and the IDE shows the message.

A test reads this document and checks that the table lists the same codes with the same messages,
so neither can drift from the other.
