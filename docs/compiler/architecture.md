# Compiler architecture

## Shape

```
                 C# sources                      C headers
                     |                               |
          +----------v-----------+        +----------v-----------+
          | Frontend.CSharp      |        | Frontend.C           |
          | Roslyn, subset rules,|        | libclang, layouts,   |
          | lowering to IR       |        | constants            |
          +----------+-----------+        +----+------------+----+
                     |                         |            |
                     |   Delta IR (generic)    |   IR decls |
                     +------------+------------+            |
                                  |                +--------v---------+
                         +--------v--------+       | Backend.CSharp   |
                         | Core            |       | import stubs ->  |
                         | instantiation,  |       | Frontend.CSharp  |
                         | comptime, safety|       +------------------+
                         | layout, verify  |
                         +--------+--------+
                                  |  Delta IR (final)
                     +------------+-------------+
                     |                          |
             +-------v--------+        +--------v---------+
             | Backend.C      |        | projection       |
             | C11 + header   |        | backends (later) |
             +-------+--------+        +------------------+
                     |
             +-------v--------+
             | Toolchain      |
             | zig cc, clang, |
             | gcc, msvc      |
             +----------------+
```

The **driver** owns the order of these steps; no component calls another component except through
the data it receives.

## Separation rules

These are invariants, checked by project references (a forbidden reference does not compile):

1. `CDelta.Ir` depends on nothing but `CDelta.Diagnostics`.
2. A frontend depends on `CDelta.Ir`, `CDelta.Diagnostics` and its own parser. It never references
   the core, a backend or another frontend.
3. `CDelta.Core` depends on `CDelta.Ir` and `CDelta.Diagnostics`. It knows no source language and
   no target language.
4. A backend depends on `CDelta.Ir` and `CDelta.Diagnostics`. It never references a frontend or
   the core: everything it needs (layouts, instantiations, constants) is in the IR it receives.
5. `CDelta.Toolchain` knows how to run C compilers and linkers; it knows nothing about the IR.
6. `CDelta.Driver` is the only project that references all of them.
7. Nothing in a frontend or backend names a concept of another language. A word that belongs to
   C# (a Roslyn symbol) does not enter the IR; it is translated at the frontend boundary.

## Projects

| Project | Target framework | Role |
|---|---|---|
| `src/CDelta.Diagnostics` | `netstandard2.0` | diagnostic codes, severities, source spans, the registry |
| `src/CDelta.Ir` | `net10.0` | Delta IR model, builder, verifier, printer, parser |
| `src/CDelta.Core` | `net10.0` | passes over IR, comptime interpreter, target descriptions |
| `src/CDelta.Frontend.CSharp.Rules` | `netstandard2.0` | subset checker, Roslyn analyzer and diagnostic suppressor; loaded by the IDE |
| `src/CDelta.Frontend.CSharp` | `net10.0` | Roslyn compilation, corelib symbols, lowering to IR |
| `src/CDelta.Frontend.C` | `net10.0` | libclang reading of headers into IR declarations |
| `src/CDelta.Backend.C` | `net10.0` | IR to C11 and the export header |
| `src/CDelta.Backend.CSharp` | `net10.0` | IR declarations to C# source: import stubs now, idiomatic bindings later |
| `src/CDelta.Projection` (M8) | `net10.0` | shared analyses of IR declarations for projection backends |
| `src/CDelta.Backend.Zig` (M8) | `net10.0` | IR declarations to idiomatic Zig bindings |
| `src/CDelta.Toolchain` | `net10.0` | C toolchain discovery and invocation, target triples |
| `src/CDelta.Driver` | `net10.0` | project model, pipeline, caches |
| `src/CDelta.Cli` | `net10.0` | the `cdc` command |
| `src/CDelta.Build.Tasks` | `netstandard2.0` | MSBuild tasks that run `cdc` out of process |
| `sdk/CDelta.Sdk` | | MSBuild SDK: props, targets, packaging of corelib, std, analyzer |
| `lib/corelib` | none: it defines `System.Object` itself | the corelib reference assembly |
| `lib/std` | CΔ | the standard library |
| `tests/*` | `net10.0` | see [testing.md](testing.md) |

`CDelta.Build.Tasks` runs `cdc` as a separate process because MSBuild loads its own Roslyn, and two
Roslyn versions in one process conflict.

## Host and portable projects

The projects split into two groups ([../decisions.md](../decisions.md), D116 to D118).

| Group | Projects | Language |
|---|---|---|
| Host | `Frontend.CSharp`, `Frontend.CSharp.Rules`, `Build.Tasks`, the host part of `Cli` | C# on .NET, permanently |
| Portable | `Diagnostics`, `Ir`, `Core`, `Frontend.C`, `Backend.C`, `Backend.CSharp`, `Projection`, `Backend.Zig`, `Toolchain`, `Driver` | C# until M7, then CΔ ([../roadmap.md](../roadmap.md#m10-self-hosting)) |

Code in a portable project follows these rules, which a test of the solution checks where it can:

- Data is structs and integer indices into arenas or lists, not graphs of objects.
- Errors are returned as values. Exceptions are only internal errors (`CD9001`).
- No LINQ, reflection, `async`, `dynamic`, events, finalizers or inheritance between its own
  classes.
- A portable project references no host project and no package other than those listed in
  [Pinned dependencies](#pinned-dependencies) for it.

After the port, the portable group is one native library, `cdelta-core`, with a C API generated by
`[Export]`. The host lowers each package to IR, passes the binary IR to the library through
`[DllImport]`, and receives diagnostics and the build result.

## Repository layout

```
c-delta/
  CDelta.slnx
  Directory.Build.props        shared settings: net10.0, nullable, warnings as errors, artifacts
  Directory.Packages.props     central package versions
  global.json                  pinned .NET SDK
  src/                         compiler projects
  sdk/CDelta.Sdk/              MSBuild SDK
  lib/corelib/                 corelib declarations
  lib/std/                     standard library
  tests/                       unit, conformance and end-to-end tests
  samples/                     example programs, built by CI
  docs/                        these documents; docs/kanban is not versioned
```

## Pinned dependencies

| Dependency | Version policy |
|---|---|
| .NET SDK | `global.json`, one major version |
| `Microsoft.CodeAnalysis.CSharp` | pinned exactly; `LangVersion` pinned to the C# version it ships. Raising it is a decision that reviews every new construct against [../language/subset.md](../language/subset.md). |
| ClangSharp and libclang runtime packages | pinned exactly, one libclang major for every platform |
| C toolchain | a minimum version per toolchain, checked at startup ([driver-toolchain-sdk.md](driver-toolchain-sdk.md)) |
| xunit | tests only |

Versions live in `Directory.Packages.props`.

## The pipeline

The driver runs these stages for one build. Each stage consumes the output of the previous ones,
and a stage that reports an error stops the build after the stage completes, so that every error
of that stage is reported together.

| # | Stage | Component | Input | Output |
|---|---|---|---|---|
| 1 | Load | Driver | project file or command line | `BuildPlan`: packages in dependency order, sources, options, target |
| 2 | Toolchain | Toolchain | target | C compiler, sysroot and include paths for the target |
| 3 | Scan imports | Frontend.CSharp | sources (syntax only) | the `[Import]` declarations |
| 4 | Read C | Frontend.C | imports, include paths, target | IR declaration module per import set |
| 5 | Stubs | Backend.CSharp | IR declarations | generated C# files, added to the package's sources |
| 6 | Bind | Frontend.CSharp | sources, corelib, dependency compilations | one Roslyn `CSharpCompilation` per package; Roslyn diagnostics |
| 7 | Check subset | Frontend.CSharp.Rules | compilations | CΔ diagnostics `CD1xxx`, `CD2xxx`, `CD3xxx`, `CD5xxx` |
| 8 | Lower | Frontend.CSharp | reachable symbols, on demand | generic IR functions and types |
| 9 | Core | Core | generic IR, roots, target | final IR |
| 10 | Generate | Backend.C | final IR | `.c` and `.h` |
| 11 | Compile and link | Toolchain | `.c`, C sources, libraries | the output |

Stages 8 and 9 interleave: the core asks the frontend for the body of a function when an
instantiation first reaches it, through an interface defined in `CDelta.Ir`
(`IBodyProvider`), so that only reachable code is lowered. The C frontend implements the same
interface for C functions that have bodies to translate (a later milestone).

## Determinism

The same inputs produce byte-identical C output. Every collection that reaches an output is
ordered by a stable key (fully qualified name, then instantiation key), never by hash order or
completion order of parallel work. Generated names never contain addresses or counters that
depend on traversal order.

## Parallelism

Everything is serial until M9 ([engineering.md](engineering.md#parallelism)). Afterwards: Roslyn
binds and checks packages in parallel within dependency order. Lowering and core passes run
per function in parallel where a pass is function-local; the instantiation worklist is the single
serial point. The C compiler is invoked once per translation unit.

## Caching

| Cache | Key | Value |
|---|---|---|
| C import | hash of headers read (transitively), defines, include paths, target, libclang version | IR declarations and generated stubs |
| C sources | hash of the source and its includes, flags, target, toolchain | object file |
| Generated C | hash of the final IR module | object file |

Caches live in `obj/cdelta/` per project and in a global cache directory for packages. Incremental
compilation of CΔ code below the module level is a later milestone; whole-program compilation is
correct first.

## Internal errors

The compiler is C#, so it uses exceptions internally. An exception that escapes a stage is caught
by the driver and reported as `CD9001` with the stage, the function being processed and the
stack trace. In compiler Debug builds, the IR verifier runs after every pass and a failure is
`CD9002`.

## Observability

| Option | Effect |
|---|---|
| `--dump-ir <stage>` | print the IR after a stage to `obj/cdelta/ir/` |
| `--emit-c` | keep the generated C |
| `--time` | time per stage |
| `--verbose` | the toolchain command lines |
