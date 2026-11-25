# Roadmap

Milestones in order. Each ends with acceptance criteria that are tests, not opinions. A milestone
is done when its criteria pass in CI on every tier 1 target. Progress is tracked in
`docs/kanban/`, not here.

## M0: Foundation

The repository builds and tests nothing yet, correctly.

- Solution, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, every project of
  [compiler/architecture.md](compiler/architecture.md) empty but referencing only what the
  separation rules allow.
- `CDelta.Diagnostics` with the registry of [compiler/diagnostics.md](compiler/diagnostics.md).
- Corelib skeleton: what Roslyn needs to bind an empty `static class` with no BCL.
- CI running build and tests in the distrobox `dev` image.

Accepted when: `dotnet build` and `dotnet test` pass; the registry test matches the document; a
test proves Roslyn binds a source against the corelib with no error.

## M1: Hello, C

The smallest end to end path.

- IR model, builder, printer, verifier.
- Frontend: static methods, `int` and `bool` locals, arithmetic in all three contexts, `if`,
  `while`, `for`, calls, `return`, `[DllImport]` externs, string literals to `byte*`.
- Core: roots, instantiation without generics, safety pass for overflow and division, layout of
  primitives, entry point.
- Backend C: everything the above needs, `#line`, the prelude, the default panic.
- Toolchain: `zig cc` and `clang` providers.
- `cdc build` and `cdc run` on files.

Accepted when: `hello` prints through `puts`; an overflow program panics in Debug and wraps under
`unchecked`; conformance runner works with `.stdout`, `.exitcode` and `.panic`.

## M2: Data

- Structs, methods, constructors, layouts, `ref`, `in`, `out`, pointers, `fixed`, `stackalloc`,
  spans and slicing, inline arrays, fixed buffers, static arrays, enums, `switch` and patterns
  except type patterns, tuples, optionals, `Defer`, `using`, `goto`.
- The subset analyzer with every `CD1xxx` and `CD2xxx` rule, and the Roslyn promotions.
- Layout assertions in generated C; the layout catalog test.
- IR parser and round trip.

Accepted when: the conformance directories `types`, `memory` and `subset` pass in all modes;
strict C and sanitizers pass on them.

## M3: Errors

- `Result` types, error sets, global error codes, `Try`, `Catch`, `Fail`, `TryGet`, `ErrDefer`,
  `Panic`, `Unreachable`, `[PanicHandler]`, `Main` forms, error return traces.
- Every `CD3xxx` rule.

Accepted when: `errors` conformance passes; a failing `Main` prints the error name and trace.

## M4: Abstraction

- Generics with monomorphization, constraints, static abstract members, generic math on
  primitives.
- Interfaces as constraints, `Dyn<I>`, dispatch tables, `[Union]` interfaces, type patterns.
- Delegates, lambdas, closure conversion, local functions, function pointers.
- Every `CD5xxx` rule, `CD2002`.

Accepted when: `generics` conformance passes; a generic `List<T>` and a `Dyn<IAllocator>` arena
work in a sample.

## M5: C import and export

- C frontend: declarations, layouts, constants, comments, unsupported declarations.
- Import stubs, `[Import]`, `[CMacro]`, C sources, native libraries, the import cache.
- `[Export]`, the export header, static and shared libraries, `--emit-c`.
- Every `CD6xxx` rule.

Accepted when: `zlib`, `sqlite`, `vulkan-headers` and `export-lib` end to end cases pass; the
layout assertions of every imported struct pass.

## M6: Comptime

- The interpreter, static initializers, `Comptime(...)`, `[Comptime]` parameters, folding and
  pruning, `TypeInfo`, `Builtin`, `Embed`, `CompileError`, quotas.
- Every `CD4xxx` rule.

Accepted when: `comptime` conformance passes; differential tests pass.

## M7: Std and SDK

- The first version of [std.md](std.md), with its own tests.
- `CDelta.Sdk`, `cdc test`, `cdc fmt`, `cdc init`, package format, NuGet packaging, the `cdelta`
  tool.
- Cross compilation to every tier 1 and 2 target.

Accepted when: `sdk`, `ide` and `cross` end to end cases pass; the std's tests pass in all modes;
a sample project opens in VS Code with completion and CΔ diagnostics.

## M8: kabic

The steps of [kabic-migration.md](kabic-migration.md).

Accepted when: kabic's end to end suites pass through `cdc project`, and KernelEngine builds with
it.

## M9: Hardening

- GCC and MSVC providers, tier 2 runners.
- Incremental compilation of the generated C by module.
- C function bodies in the C frontend (comptime of `static inline` C functions).
- Documentation of the std in the generated reference.

Accepted when: the conformance suite passes on every toolchain of every tier 1 target and the
toolchain differential test passes.
