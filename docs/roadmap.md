# Roadmap

Milestones in order: M0, M1, M2, M3, MVP, M4 and onward. Each ends with acceptance criteria that are tests, not opinions. A milestone
is done when its criteria pass in CI on every tier 1 target.

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
- Getter-only static abstract properties and their implementations (D153), `CD1004` narrowed.
- `CD2002` over values holding capturing lambdas (D154).
- Top-level statements (D155), `CD1033`, `CD1022` retired; `Process.Run` with the first std.
- `string` as UTF-8 text (D148): the corelib struct, equality, string `switch` and patterns,
  `foreach` over runes, `Rune`, `Startup.Args()` and `Process.Args()` as strings, `CD1031`,
  `CD1032`, `CD1020` retired.
- Every `CD5xxx` rule, `CD2002`.

Accepted when: `generics` conformance passes; a generic `List<T>` and a `Dyn<IAllocator>` arena
work in a sample.

## M5: C import and export

- The rest of the C frontend: comments into doc comments, doc tags and annotations, the import
  cache.
- `[CMacro]`, `CSource` items, macros of constant addresses.
- `[Align]`, `[Naked]`, `[Interrupt]`, `[NoAlias]`, `AsmSource` and `CppSource` items, `EntrySymbol`,
  `LinkerScript`, `LinkerArgs`, the embedded targets ([language/c-parity.md](language/c-parity.md)).
- `[Export]`, the export header, static and shared libraries, `--emit-c`; the CMake and
  `pkg-config` files of libraries and the `CMakeLists.txt` of the C source output (D165).
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
- Cross compilation to every tier 1 and 2 target; the `auto` order and toolchain packs with
  `cdc toolchain install` (D108, D163).
- Dependencies (D164): the `CDelta` package type, lock files, `CDeltaFetch` with recipes for C,
  `cdc fetch --save`, `PkgConfig` on `NativeLibrary`, `cdc publish`, `CD0010`.
- Build speed (D158): a compiler server, `cdc watch`, that keeps Roslyn, the imports and the core's
  state warm; the generated C split by module, with object files cached by the SHA-256 of their C;
  a spike publishing `cdc` with NativeAOT, which decides whether builds can run without the .NET
  SDK; the `tcc` provider for `Debug` builds (D175).
- Debugging (D159): the formatters of [compiler/backend-c.md](compiler/backend-c.md#debug-information)
  and a launch configuration written by `cdc init`.

Accepted when: `sdk`, `ide` and `cross` end to end cases pass; the std's tests pass in all modes;
a sample project opens in VS Code with completion and CΔ diagnostics; a one-line change to a
sample of a few thousand lines rebuilds and runs in Debug in under one second on the CI machine;
the `debug` end to end case inspects a `string`, a `Result` and a `[Union]` value in GDB and
LLDB.

## M8: kabic

The steps of [kabic-migration.md](kabic-migration.md).

- The clean room of the annotation vocabulary (D179), then the CΔ projection, then the C#
  projection as its shape over the BCL.
- The plugin protocol of projection backends (D167) and the version of the declaration layer of
  the IR (D168).
- Hot reload (D160): a shared library reloaded by its host on change, with versioned file names;
  state in host or arena memory survives; a changed layout of a state struct, found by comparing
  `TypeInfo`, migrates the fields kept by name or refuses the reload.

Accepted when: the vocabulary of the clean room and the review of kabic's pieces are recorded; the probe, zlib, sqlite and vulkan end to end suites pass through `cdc project`; KernelEngine builds with it;
an official KernelEngine part written in CΔ runs in the engine and is reloaded while the engine
runs after a change to its source.

## M9: Hardening

- GCC and MSVC providers, tier 2 runners.
- C function bodies in the C frontend (comptime of `static inline` C functions).
- `Asm`, `FrameAddress`, `ReturnAddress`, `MustTail` and the `Vector64<T>` to `Vector512<T>` types,
  all designed first
  ([language/c-parity.md](language/c-parity.md)).
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

## 1.0

1.0 is not a milestone of features. It is declared by the owner after M8 and M10 are accepted and
after a period of months in which CΔ is used in real projects, KernelEngine among them, until no
change to the subset, to CΔ's rules or to the std is pending that would need a new edition. Until
then CΔ is experimental and edition `1` is still open: its rules may change between releases.
From 1.0 on, edition `1` is closed and every rule of
[language/modules-build.md](language/modules-build.md#editions) applies.

## Later

Not scheduled; each starts when a real program needs it, with its gaps reviewed first.

- **C++ libraries** (D172): the C frontend reads C++ through libclang and `cdc` generates an
  `extern "C"` shim compiled as `CppSource`: objects as opaque handles, constructors and
  destructors as functions, exceptions caught at the boundary as error codes, conversions for the
  C++ standard library types at the boundary. Templates are instantiated on demand: the instances
  the CΔ code uses (`std::vector<Player>`, with CΔ structs visible to C++ through the generated
  header) are generated in the shim, plus any a manifest names.
  The shim is imported as C and projected; C and Zig can use it as well.
- **Rust libraries** (D172): through uniffi. A crate that uses uniffi is read from its metadata
  and called through the boundary uniffi generated; for one that does not, `cdc` generates a
  wrapper crate that depends on it and marks the wanted functions and types with
  `#[uniffi::export]`, with concrete instances of its generics, and cargo builds it. A boundary
  of CΔ's own, without uniffi's serialization, only if performance requires it.
- **Interface importers** (D172): readers of the interface descriptions other languages already
  produce (rustdoc JSON, Nim's exported headers) into IR declarations and annotations, so their
  libraries are consumed idiomatically by CΔ, C# and every projection plugin.
- **The DOM** (D160): `[WasmImport(module, name)]` for JavaScript functions on `wasm32`, and a
  TypeScript projection backend that writes the JavaScript glue from the IR's imports and exports.

- **Iterators** (D151): `yield return` in a method returning `IEnumerable<T>` or `IEnumerator<T>`
  becomes a struct state machine generated by the frontend: locals live across a `yield` become
  fields, each `yield` a state, and the state lives in the caller's frame with no allocation. The
  result may only be iterated with `foreach` or held in a `var` local; converting it to the
  interface type is an error, except through `Dyn<IEnumerable<T>>`.
- **Concurrency** (D152), in three steps: `Std.Io`, an interface passed like `IAllocator` with
  `Async` returning a future struct and `Await`, over a thread pool; an evented backend over the
  OS's readiness or completion API with fixed-size stacks from an allocator and a context switch
  per target; and only if a program still needs it, `async` and `await` as the iterator
  transform, where an async frame is a struct of known size that never moves once started, so
  recursive async is an error and starting a task takes an allocator.
