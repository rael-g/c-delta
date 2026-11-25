# Driver, toolchain and SDK

## `cdc`

`cdc` is the compiler command. Every other entry point (MSBuild, the IDE, tests) runs it.

| Command | Does |
|---|---|
| `cdc build [project.csproj or files.cs]` | build the output |
| `cdc run [project or file] [-- args]` | build and run an executable |
| `cdc test [project] [--filter text]` | build and run the tests |
| `cdc cimport [project]` | generate the import stubs only; the SDK runs it before every compile, including the IDE's design-time builds |
| `cdc refasm [project]` | write the Roslyn reference assembly of a package, for publishing |
| `cdc ir [project] --stage lowered or instantiated or final` | print the IR |
| `cdc fmt [paths] [--check]` | format sources with Roslyn's formatter and the one CΔ style |
| `cdc init [--lib]` | create a project |
| `cdc targets` | list supported targets and their tiers |
| `cdc toolchain` | show the C toolchain chosen for a target and its paths |
| `cdc version` | compiler, corelib, Roslyn, libclang and toolchain versions |

Common options: `--target`, `--mode`, `-o`, `--emit-c`, `--dump-ir <stage>`, `--toolchain`,
`--option name=value`, `--verbose`, `--time`, `--color`.

Given `.cs` files instead of a project, `cdc` builds them as one executable package with the
default properties, so a single file program needs no project file.

Exit codes: `0` success, `1` compile errors, `2` invalid invocation, `3` internal error.

Diagnostics are printed as `path(line,col): error CD1001: message`, the format MSBuild and every
C# IDE parse.

## The driver

`CDelta.Driver` owns the pipeline of [architecture.md](architecture.md#the-pipeline).

- **Project model.** A `BuildPlan` is built from a `.csproj` by asking MSBuild for its evaluated
  properties and items (`dotnet msbuild -getProperty -getItem` on the project, which needs no
  MSBuild in process), or from the command line for single files. Package references are resolved
  from `project.assets.json`, written by restore.
- **Packages.** Each package in the plan has its sources, C items, properties and references. The
  plan is ordered topologically; a cycle is `CD0001`.
- **Options.** A single immutable `CompilerOptions` record built from the plan is the only
  configuration any stage reads.

## C toolchain

`CDelta.Toolchain` hides which C compiler is used behind one interface: compile a C file, link
objects into an executable or shared library, archive a static library, and report the system
include directories for a target.

| Provider | Found by | Targets |
|---|---|---|
| `zig` | `CDELTA_ZIG`, then `zig` on `PATH` | every supported target, with its C library, from any host |
| `clang` | `CDELTA_CLANG`, then `clang` on `PATH` | the host, and other targets when a sysroot is given |
| `gcc` | `CDELTA_GCC`, then `gcc` on `PATH` | the host |
| `msvc` | `vswhere` on Windows | `*-windows-msvc` |

`CToolchain=auto` takes the first one found in that order that supports the target. `zig cc` is the
default because it cross-compiles to every target with no setup, which is the lazy way to make
cross compilation the normal case.

- System include directories are read from the compiler itself (`-E -v` on an empty input) and
  given to libclang, so imports see the same headers the C compiler uses.
- Each provider has a minimum version, checked when it is selected (`CD0002`).
- Command lines are shown with `--verbose`; a failing C compilation of generated C is `CD0003`
  (always a compiler bug), of a user C source `CD0004`, a failing link `CD0005`.

### Target tiers

| Tier | Targets | Promise |
|---|---|---|
| 1 | `x86_64-linux-gnu`, `aarch64-linux-gnu`, `x86_64-windows-gnu` | built and tested by CI on every change, tests executed |
| 2 | `x86_64-linux-musl`, `aarch64-macos-none`, `x86_64-windows-msvc`, `wasm32-wasi-musl` | built by CI on every change, tests executed when a runner exists |
| 3 | `*-freestanding-none` | built by CI |

## The MSBuild SDK

`CDelta.Sdk` makes a `.csproj` a CΔ project for `dotnet build` and for every C# IDE.

### `Sdk.props`

- Imports the standard .NET SDK props, then sets `TargetFramework` to `net10.0` with
  `DisableImplicitFrameworkReferences`, `NoStdLib`, `NoCompilerStandardLib`, `ImplicitUsings`
  disabled and `Nullable` disabled, so Roslyn sees no BCL.
- References the corelib and the std reference assemblies shipped in the SDK.
- Adds the global usings file of [frontend-csharp.md](frontend-csharp.md#roslyn-compilation).
- Adds the subset analyzer and suppressor.
- Sets `LangVersion`, `AllowUnsafeBlocks`, the promoted warnings in `WarningsAsErrors` and the
  suppressed ones in `NoWarn`.
- Sets `DefineConstants` to the build symbols of the target and mode, so the IDE evaluates `#if`
  as the build does.

### `Sdk.targets`

| Target | Hooks | Does |
|---|---|---|
| `CDeltaImports` | before `BeforeCompile`, also in design-time builds | runs `cdc cimport` with inputs and outputs for incrementality; adds the stubs to `Compile` |
| `CoreCompile` | replaced when not a design-time build | runs `cdc build` through the `Cdc` task with the evaluated items and properties |
| design-time `CoreCompile` | unchanged | the IDE still gets the Csc command line it needs for its language service |
| `Run` | `RunCommand` | points `dotnet run` at the native output |
| `VSTest` | replaced | runs `cdc test`, so `dotnet test` works |
| `Pack` | extended | packs a CΔ package |

Outputs go to `bin/<Configuration>/<Target>/`.

### Package format

A CΔ NuGet package contains:

| Path | Contents |
|---|---|
| `cdelta/src/**` | the package's CΔ sources |
| `cdelta/native/include/**`, `cdelta/native/src/**` | its C headers and C sources |
| `cdelta/package.props` | its C items, defines and native libraries |
| `ref/net10.0/<Name>.dll` | its Roslyn reference assembly, for the IDE of consumers |
| `build/<Name>.props` | adds the above to a consumer's build |

The consumer's IDE binds against the reference assembly; the consumer's `cdc` compiles the
sources, so whole-program compilation holds across packages.

## Distribution

- `CDelta.Sdk` on NuGet contains `cdc` (framework-dependent on the .NET that runs MSBuild), the
  corelib, the std sources and reference assembly, and the analyzer.
- The `cdelta` .NET tool installs the same `cdc` for command-line use.
- Neither contains a C toolchain; `cdc toolchain` explains what is missing.

## IDE

What works with no IDE plugin, because CΔ is C#:

- syntax highlighting, completion, navigation, find references, rename and refactorings;
- Roslyn errors and CΔ diagnostics as you type, through the analyzer;
- C import declarations, through the design-time stubs, with C doc comments;
- formatting with `cdc fmt` or the IDE's C# formatter;
- debugging the native output with a native debugger (GDB, LLDB, the Visual Studio debugger),
  stepping through `.cs` sources thanks to `#line`.

`samples/` includes a VS Code launch configuration for LLDB.
