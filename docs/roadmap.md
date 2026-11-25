# Roadmap

Milestones in order: M0, M1, M2, M3, MVP, M4 and onward. Each ends with acceptance criteria that are tests, not opinions. A milestone
is done when its criteria pass in CI on every tier 1 target. Progress is tracked in
`docs/kanban/`, not here.

## M0: Foundation

The repository builds and tests nothing yet, correctly.

- Solution, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, every project of
  [compiler/architecture.md](compiler/architecture.md) empty but referencing only what the
  separation rules allow.
- `CDelta.Diagnostics` with the registry of [compiler/diagnostics.md](compiler/diagnostics.md).
- Corelib skeleton: what Roslyn needs to bind the probes, with every type of
  [compiler/frontend-csharp.md](compiler/frontend-csharp.md#the-corelib) the probes touch.
- The settings, packages and `.editorconfig` of [compiler/engineering.md](compiler/engineering.md).
- The Roslyn probes of [compiler/engineering.md](compiler/engineering.md#roslyn-probes).
- `eng/ci.sh` and `eng/setup-dev.sh`.

Accepted when: `eng/ci.sh` passes; the registry test matches the document; every probe passes.

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
- Global initializers that are constants or collection expressions of constants. Until M6 any
  other initializer is `CD4006`.
- Operators and explicit conversions on user types, extension operators, lifted operators on
  optionals (D143).
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

## MVP

The first version someone can use: command line tools over C libraries, with errors as values and
deferred cleanup. Version `0.1.0`.

- Everything of M0 to M3.
- C frontend for: functions (variadic included), `extern` globals, structs, unions, incomplete
  structs, enums, typedefs, object-like macros that evaluate to constants, and `[Unsupported]` for
  the rest. No doc tags, annotations or cache: every build parses the headers.
- Import stubs, `[Import]` with `Defines`, `IncludeDirs` and `Only`, the `CDeltaImports` target
  for the IDE, `NativeLibrary` items, linking.
- The rules of these features: `CD6001`, `CD6002`, `CD6003`, `CD6007`, `CD6009`.
- The MVP subset of the std ([std.md](std.md#the-mvp-subset)).
- `Result<T>`, `Result<T, E>`, `Span<T>`, `ReadOnlySpan<T>` and `Nullable<T>` work because the
  frontend maps them to the IR's own `result`, `slice` and `optional` types; user generics are
  still M4.
- `samples/count-lines`:

```csharp
using Std;

[Import("stdio.h")]
static unsafe partial class C { }

enum CountError { CannotOpen, ReadFailed }

static unsafe class Program
{
    static Result<long, CountError> CountLines(byte* path)
    {
        var file = C.fopen(path, "rb");
        if (file == null) return CountError.CannotOpen;
        Defer(() => C.fclose(file));

        long lines = 0;
        int ch;
        while ((ch = C.fgetc(file)) != C.EOF)
            if (ch == '\n') lines++;
        if (C.ferror(file) != 0) return CountError.ReadFailed;
        return lines;
    }

    static Result Main()
    {
        var args = Process.Args();
        if (args.Length != 2)
        {
            Debug.Print("usage: count-lines <file>\n");
            Process.Exit(2);
        }
        var lines = Try(CountLines(args[1].Pointer));
        C.printf("%lld\n", __arglist(lines));
        return default;
    }
}
```

Accepted when: `count-lines` built by `cdc build` prints the line count of a file on
`x86_64-linux-gnu`, `aarch64-linux-gnu` and `x86_64-windows-gnu`, and for a missing file prints
`error: CannotOpen` with a trace and exits `1`; its output links nothing but libc; the conformance
directory `interop` passes for the imported declarations above; the sample opens in VS Code with
completion of `C.fopen` and CΔ diagnostics.

## M4: Abstraction

- Generics with monomorphization, constraints, static abstract members and operators, generic
  math on primitives and on user types.
- Interfaces as constraints, `Dyn<I>`, dispatch tables, `[Union]` interfaces, type patterns.
- Delegates, lambdas, closure conversion, local functions, function pointers (D144) and `CD1030`.
- Indexer getters on user types (D150), `CD1005` narrowed to setters.
- `string` as UTF-8 text (D148): the corelib struct, equality, string `switch` and patterns,
  `foreach` over runes, `Rune`, `Startup.Args()` and `Process.Args()` as strings, `CD1031`,
  `CD1032`, `CD1020` retired.
- Every `CD5xxx` rule, `CD2002`.

Accepted when: `generics` conformance passes; a generic `List<T>` and a `Dyn<IAllocator>` arena
work in a sample.

## M5: C import and export

- The rest of the C frontend: comments into doc comments, doc tags and annotations, the import
  cache.
- `[CMacro]`, `CSource` items.
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

- The first version of [std.md](std.md), with its own tests, including the `string` members of
  `Std.Text`.
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

## M10: Self-hosting

The portable projects of [compiler/architecture.md](compiler/architecture.md#host-and-portable-projects)
become CΔ, one at a time, in this order: `Backend.C`; the IR model, printer, parser and verifier;
the core passes; the comptime interpreter; `Toolchain` and `Driver`; `Frontend.C` over
`clang-c/Index.h`; `Backend.CSharp` and the projection backends.

- The binary IR format and the `cdelta-core` C API.
- While a component exists in both languages, both run in CI and must give identical output on
  every golden file and conformance case.
- A C# component is deleted when its CΔ version passes alone.

Accepted when: every portable project is CΔ; the compiler built by itself produces C identical to
the compiler built by the previous release on the whole conformance suite; the host contains only
the projects listed as host.

## Later

Not scheduled; each starts when a real program needs it, with its gaps reviewed first.

- **Iterators** (D151): `yield return` in a method returning `IEnumerable<T>` or `IEnumerator<T>`
  becomes a struct state machine generated by the frontend: locals live across a `yield` become
  fields, each `yield` a state, and the state lives in the caller's frame with no allocation. The
  result may only be iterated with `foreach` or held in a `var` local; converting it to the
  interface type is an error, except through `Dyn<IEnumerable<T>>`.
- **Async** (D152), after iterators: the same transform, with `await` as the visible suspension
  point. An async frame is a struct value of a size known at compile time, so recursive async is
  an error; starting a task takes an allocator; task types are structs with their own builder;
  the executor and the event loop over the OS's I/O readiness or completion API are std code, not a
  runtime. The design is preceded by a study of how other languages do it.
