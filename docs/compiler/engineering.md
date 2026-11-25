# Engineering

How the compiler itself is built, tested and written. Everything here is fixed; a need this
document does not cover is a question for the design, not a choice of the implementer
([../../AGENTS.md](../../AGENTS.md#working-on-a-milestone)).

## Environment

Everything runs in the distrobox `dev`. It provides:

| Tool | Version | Use |
|---|---|---|
| .NET SDK | 10.0, the feature band pinned by `global.json` | building and testing the compiler |
| `zig` | 0.15.1 or newer | the default C toolchain |
| `clang` | 18 or newer | the alternative toolchain, the toolchain differential test |
| `gcc` | 13 or newer | the toolchain differential test |
| `qemu-aarch64` and an aarch64 glibc sysroot | any | running `aarch64-linux-gnu` tests; the sysroot path is in `CDELTA_AARCH64_SYSROOT` |
| `wine` | 9 or newer | running `x86_64-windows-gnu` tests |
| `git`, `bash` | any | |

These minimum versions are also the ones `CD0002` checks (`msvc`: Visual Studio 2022 17.8 or newer).

`global.json` pins `sdk.version` to the newest 10.0 feature band available at M0 with
`rollForward` set to `latestPatch`.

## Packages

Package versions are central (`Directory.Packages.props`, `ManagePackageVersionsCentrally`). At
M0 each is pinned to the newest stable release of the line given here; later upgrades are
decisions.

| Package | Line | Projects |
|---|---|---|
| `Microsoft.CodeAnalysis.CSharp` | 5.0.x, the line whose newest language version is C# 14 | `Frontend.CSharp`, `Frontend.CSharp.Rules` (with `PrivateAssets=all`), their tests |
| `ClangSharp` and its `libClangSharp.runtime.*` and `libclang.runtime.*` packages for `linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64` | the newest stable major, all of the same version | `Frontend.C` |
| `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | newest stable | tests |

No other package is added without a decision. In particular there is no command line library (the
`cdc` parser is written by hand in `CDelta.Cli`), no assertion, mocking or snapshot library, and no
YAML library before M8.

If the 5.0.x line of Roslyn does not support exactly C# 14, stop: that is a question.

## Solution and project settings

`CDelta.slnx` contains every project under `src/`, `lib/` and `tests/`. `Directory.Build.props` at
the root sets, for every project:

| Property | Value |
|---|---|
| `LangVersion` | `14` |
| `Nullable` | `enable` (compiler projects only; `lib/` projects set their own) |
| `ImplicitUsings` | `enable` (compiler projects only) |
| `TreatWarningsAsErrors` | `true` |
| `AnalysisLevel` | `latest` |
| `UseArtifactsOutput` | `true`, so outputs go to `artifacts/` |
| `Deterministic` | `true` |
| `InvariantGlobalization` | `true` |
| `Version` | `0.0.0` until the MVP, `0.1.0` at the MVP, then the minor version grows by one per milestone |

Project names are the names of [architecture.md](architecture.md#projects) with the `CDelta.`
prefix, which is also the root namespace and the assembly name. Test projects are named after the
project they test with `.Tests`.

### The corelib project

`lib/corelib/CDelta.Corelib.csproj` builds the corelib with Roslyn and no BCL:

- `TargetFramework` `net10.0`, `DisableImplicitFrameworkReferences`, `NoStdLib`,
  `NoCompilerStandardLib`, `GenerateAssemblyInfo` `false`, `ImplicitUsings` `disable`, `Nullable`
  `disable`, `AllowUnsafeBlocks` `true`, `ProduceOnlyReferenceAssembly` `true`;
- assembly name `CDelta.Corelib`, version equal to the compiler version; `CD0008` compares both.

### The std project

`lib/std/CDelta.Std.csproj` is a CΔ project: before M7 it imports `sdk/CDelta.Sdk/Sdk/Sdk.props`
and `Sdk.targets` directly by path, after M7 it uses `CDelta.Sdk`. Its assembly name is `Std`.
The driver adds it to every build as an implicit dependency of every package; unused std code is
never compiled to C because only reachable code is lowered.

### Where `cdc` finds its files

`CDelta.Cli` copies into its output directory, as build content:

```
lib/corelib/CDelta.Corelib.dll
lib/std/**/*.cs
```

`cdc` finds them relative to `AppContext.BaseDirectory`, or under `CDELTA_LIB` when that
variable is set. The analyzer is a project reference of `CDelta.Driver`, loaded in process.

## Writing code

### All projects

- File-scoped namespaces; the namespace is the project's root namespace plus the folder path.
- One top-level type per file, the file named after the type.
- Classes are `sealed` unless they are `static`.
- Private fields `_camelCase`; everything else follows the .NET naming guidelines.
- `var` when the type is visible on the same line; otherwise the type name.
- No comments except doc comments that state what a caller relies on; no `#region`, `#nullable`
  or `#pragma warning`. A magic number is a named constant.
- `.editorconfig` at the root encodes these rules and the build enforces them with
  `EnforceCodeStyleInBuild`.

### Portable projects

The rules of [architecture.md](architecture.md#host-and-portable-projects), with these
clarifications:

- Data is `record struct` or `struct`. A `sealed class` is allowed only for an owner of tables
  (`Module`, `Body`, a builder, a pass, a cache), never for an element of a table.
- Interfaces are allowed and are used as constraints or as the boundary between components
  (`IBodyProvider`, `IToolchainProvider`); a class implements at most the interfaces it needs and
  never inherits from another class.
- BCL collections (`List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`) and BCL I/O, process and
  hashing APIs are allowed: the port replaces them with the std.
- An operation that can fail because of the user's input returns a value or reports into a
  `DiagnosticBag`; only a compiler bug throws.

### Host projects

Ordinary C#. The Roslyn object model is used as Roslyn intends.

## Parallelism

Everything runs on one thread until M9. The parallelism of
[architecture.md](architecture.md#parallelism) is a later optimization, which determinism
already allows.

## Hashes

- Generated C names: FNV-1a as in [backend-c.md](backend-c.md#names).
- Cache keys and file identities: SHA-256, written as lowercase hex.

## Tests

- xunit v3. Each test project mirrors the folders of the project it tests.
- **Golden files** live in `tests/<Project>.Tests/Golden/<Area>/`, one input and one expected file
  per case with the same name (`try-propagates.cs` and `try-propagates.ir`, `vec2.ir` and
  `vec2.c`). A shared helper compares the output with the expected file and, when
  `CDELTA_UPDATE_GOLDEN=1`, writes it instead.
- **Analyzer tests** do not use the Roslyn analyzer testing packages, which assume the BCL. A
  helper compiles a source against the corelib, runs the analyzer and the suppressor through
  `CompilationWithAnalyzers`, and compares the diagnostics with a `.diagnostics` file in the
  conformance format.
- **Conformance** is the project `tests/CDelta.Conformance.Tests`, an xunit theory over
  `tests/conformance/**/*.cs`, one case per file, mode and target. `tests/conformance/run.sh`
  runs it with `dotnet test` and passes `--mode`, `--target` and `--filter` as test run
  parameters.
- **End to end** cases are directories `tests/e2e/<case>/` with a `test.sh` that exits `0` on
  success; `tests/e2e/run.sh` runs every case, or the ones named as arguments.
- **Running a foreign target's program**:

| Target | Command |
|---|---|
| the host | the program itself |
| `aarch64-linux-gnu` on an x86_64 host | `qemu-aarch64 -L $CDELTA_AARCH64_SYSROOT <program>` |
| `x86_64-windows-gnu` on Linux | `wine <program>.exe` |
| any other | built, not run |

## Roslyn probes

Some of the design relies on how Roslyn binds sources against our own corelib. M0 adds
`tests/CDelta.Frontend.CSharp.Tests/Probes/`, one test per assumption, each binding a small source
against the corelib and asserting there is no error. Lowering reads Roslyn's bound operations,
never its IL emitter, so only binding matters.

| Probe | Source |
|---|---|
| empty module | `static class M { }` |
| string to span | `ReadOnlySpan<byte> s = "abc";` |
| string to pointer | `byte* p = "abc";` in an `unsafe` context |
| span as a field | `struct S { public Span<int> Items; }` and `S*` |
| stackalloc | `Span<int> s = stackalloc int[4];` |
| collection expression | `Span<int> s = [1, 2, 3];` |
| span iteration and ranges | `foreach (var x in s)`, `s[1..]`, `s[^1]` |
| result conversions | `Result<int, E> F() => 1;` and `=> E.A;` |
| defer and try | `Defer(() => G());`, `var v = Try(F());` |
| panic | `throw Panic("x");` as the last statement of a non-void method |
| variadic extern | `static extern int printf(byte* f, __arglist);` and a call |
| extern without attribute | `static extern int f(int x);` binds, with CS0626 suppressed |
| attribute arrays | `[Import("a.h", Defines = ["X"])]` |

A probe that fails stops the milestone: it goes to the questions file with the Roslyn diagnostic,
and nothing is changed in the corelib or the subset to make it pass.

## CI

The repository has no remote yet. CI is `eng/ci.sh`, run in the distrobox `dev`:

1. `dotnet build CDelta.slnx` and `dotnet test CDelta.slnx`;
2. conformance in all four modes on the host, then on `aarch64-linux-gnu` and
   `x86_64-windows-gnu` through the runners above;
3. strict C and sanitizers ([testing.md](testing.md#strict-c));
4. end to end cases.

"Passes in CI" in [../roadmap.md](../roadmap.md) means `eng/ci.sh` exits `0`. Each step only
runs the parts that exist at the current milestone. When the repository gets a remote, a hosted
workflow runs `eng/ci.sh` in the same image, unchanged.
