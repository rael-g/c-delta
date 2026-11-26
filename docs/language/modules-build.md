# Modules, packages and builds

## Namespaces and modules

- Namespaces organize names, as in C#. `using`, `using static` and aliases bring them into scope.
- A `static class` is a module: functions, globals and nested types. It is the unit `using static`
  imports.
- Every source file of a package is compiled together; order of files does not matter.

## Visibility

| Modifier | Visible to |
|---|---|
| `public` | the package and every package that depends on it |
| `internal` (default for types) | the package |
| `private` (default for members) | the containing type |
| `file` | the source file |

Fields are always `public` (`CD1025`): there is no encapsulation of data. Constants (`const`)
are not data and take any visibility. A struct that wants a
narrow surface exposes functions and documents which fields are its contract.

## Packages

A package is one project file and the sources under its directory. It builds to an executable, a
library, or nothing (a source-only library consumed by others).

Compilation is whole-program: a build compiles the sources of the root package and of every
package it depends on, transitively, in one run of the compiler. There is no binary package
format for CΔ code, so generics, comptime and inlining cross package boundaries with no
restriction. Each package is still bound by Roslyn as its own compilation, so `internal` holds
between packages.

There is no CΔ ABI: the only binary contract is the C ABI of `[Export]` (D162). A library shipped
as a compiled static or shared library is consumed through its generated header, by CΔ with
`[Import]` like any C library, and its idiomatic surface (spans, `Result`) is recovered by the CΔ
projection backend ([../kabic-migration.md](../kabic-migration.md#what-cδ-gains)). Rebuilding
unchanged dependencies costs little because their generated C, and therefore their cached
objects, do not change ([../compiler/driver-toolchain-sdk.md](../compiler/driver-toolchain-sdk.md)).

Dependencies are ordinary MSBuild references, or fetched by URL:

| Dependency | How |
|---|---|
| a package in the same repository | `<ProjectReference Include="../Lib/Lib.csproj" />` |
| a published package | `<PackageReference Include="Name" Version="1.2.3" />`, restored from NuGet |
| anything at a URL: a CΔ package not published, a C library, a git commit | `<CDeltaFetch Include="name" Url="..." Hash="sha256-..." />` |

A published CΔ package carries its sources, its C headers and C sources, and a Roslyn reference
assembly for the IDE. The format is in [../compiler/driver-toolchain-sdk.md](../compiler/driver-toolchain-sdk.md).

### Resolution

- The SDK turns on NuGet lock files (`RestorePackagesWithLockFile`), so every restore is
  reproducible; CI restores in locked mode. The lock records each package's hash.
- A build has one version of each package: whole-program compilation would make two collide.
  NuGet unifies versions; a conflict it cannot unify is `CD0001`.
- A C library is best shipped inside a CΔ package (its sources as `CSource` items and its
  `[Import]` class, with a projection on top) or fetched by URL; linking a system library through
  `NativeLibrary` stays possible.

### Fetched dependencies

A `CDeltaFetch` item names an archive (`.tar.gz`, `.tar.xz`, `.zip`) or a `git+https://...#<commit>`
URL and the SHA-256 of its content (D164). It is downloaded once into a cache addressed by that
hash; the hash is its lock, and content that does not match is `CD0010`.

- If the content has a project file at its root, it is a CΔ package and a dependency like a
  project reference.
- Otherwise it is a C library, built by the metadata of the item: `Sources` (globs of C files to
  compile), `IncludeDirs`, `Defines`, and `Import` (the headers of a generated `[Import]` class
  named after the item). A longer recipe lives in a `.props` file named by `Recipe`, holding the
  same items.
- `cdc fetch <url> --save` adds the item with the hash it computed. The same name and hash reached
  twice is one dependency; the same name with two hashes is `CD0001`.

### Editions

An edition fixes the C# language version a package is bound with and the version of CΔ's own
rules: the subset, the intrinsics, the attributes and the diagnostics (D161).

| Edition | C# `LangVersion` | Introduced |
|---|---|---|
| `1` | `14`, or the newest reviewed C# version when 1.0 is declared | 1.0 |

- Every package of a build is bound by Roslyn as its own compilation with its own edition's
  `LangVersion` and rules, and all of them meet in Delta IR. A package of one edition depends on
  packages of any other.
- A compiler supports every edition before its newest, for ever. A new edition follows every C#
  version, as soon as its constructs have been reviewed (D003); there is no preview edition and no
  long-term edition, the newest is the one to use. A new edition is the only place where a rule
  of CΔ may change incompatibly; `cdc fix --edition <n>` rewrites a package to it with the code fixes of
  the rules that changed.
- Raising a package's edition is its author's choice; `cdc init` writes the newest.
- Compatibility goes forward through the compiler: a package of edition 1 that depends on a
  package of edition 2 builds with any compiler that knows edition 2, and stays edition 1 itself.
  It consumes the dependency's declarations like any others; using a construct of the newer C#
  in its own code needs its own edition raised, which Roslyn reports. A compiler that does not
  know an edition reports `CD0009`. There is no binary format between CΔ packages, so there is no
  CΔ ABI to break; the only binary contract is the C ABI of exports.
- These guarantees start at 1.0. The 0.x releases promise no compatibility with each other or
  with 1.0: whatever was deprecated or replaced before 1.0 is deleted when 1.0 is declared, and
  edition `1` is the language as it is then, with no trace of its history.

## The project file

```xml
<Project Sdk="CDelta.Sdk/0.1.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <CDeltaTarget>x86_64-linux-gnu</CDeltaTarget>
  </PropertyGroup>

  <ItemGroup>
    <CSource Include="native/**/*.c" Flags="-DFAST_PATH" />
    <CIncludeDir Include="native/include" />
    <NativeLibrary Include="z" Linkage="Static" />
  </ItemGroup>
</Project>
```

It is an MSBuild SDK-style project with the `.csproj` extension, so every C# IDE opens it.

### Properties

| Property | Values | Default |
|---|---|---|
| `CDeltaEdition` | an edition of [the table below](#editions) | the newest edition the compiler knows |
| `OutputType` | `Exe`, `Library`, `None` | `Exe` |
| `LibraryKind` | `Static`, `Shared` | `Static` |
| `CDeltaTarget` | a target triple, or `native` | `native` |
| `OptimizeMode` | `Debug`, `ReleaseSafe`, `ReleaseFast`, `ReleaseSmall` | `Debug` for `Configuration=Debug`, `ReleaseSafe` for `Release` |
| `LinkLibC` | `true`, `false` | `true` unless the target OS is `freestanding` |
| `CToolchain` | `zig`, `clang`, `gcc`, `msvc`, `auto` | `auto` |
| `EmitC` | `true` to keep the generated C next to the output | `false` |
| `PackageName` | the C-visible name of the package (header name, library name) | the project name |
| `ErrorReturnTrace` | `true`, `false` | `true` in safe modes |
| `DebugSymbols` | `true`, `false` | `true` in Debug and ReleaseSafe |
| `EntrySymbol` | the symbol the linker starts at, for freestanding targets | the toolchain's default |
| `LinkerScript` | a linker script, relative to the project | none |
| `LinkerArgs` | extra arguments passed to the linker unchanged | none |

### Items

| Item | Metadata | Meaning |
|---|---|---|
| `Compile` | | CΔ sources; by default every `*.cs` under the project directory |
| `CSource` | `Flags` | C sources compiled and linked |
| `AsmSource` | `Flags` | assembly sources (`.s`, `.S`) assembled by the toolchain and linked |
| `CppSource` | `Flags` | C++ sources compiled by the toolchain's C++ compiler (`clang++`, `g++`, `zig c++`, `cl`) and linked with its C++ library; CΔ reaches them only through `extern "C"` declarations |
| `CIncludeDir` | | include directories for imports and C sources |
| `CDefine` | `Value` | macros for imports and C sources |
| `NativeLibrary` | `Linkage` (`Static`, `Dynamic`), `PkgConfig` | libraries to link; with `PkgConfig="name"`, flags and paths come from `pkg-config` |
| `CDeltaFetch` | `Url`, `Hash`, and for a C library `Sources`, `IncludeDirs`, `Defines`, `Import`, `Recipe` | a dependency fetched by URL, [below](#fetched-dependencies) |
| `NativeLibraryDir` | | library search directories |
| `BuildOption` | `Value`, `Type` | a value read by `Builtin.Option<T>(name)` |

## Targets

A target is `<arch>-<os>-<abi>`: `x86_64-linux-gnu`, `x86_64-linux-musl`, `aarch64-linux-gnu`,
`x86_64-windows-gnu`, `x86_64-windows-msvc`, `aarch64-macos-none`, `wasm32-wasi-musl`,
`x86_64-freestanding-none`, and the embedded targets `thumbv6m-none-eabi`, `thumbv7em-none-eabi`,
`thumbv7em-none-eabihf` and `riscv32imac-none-elf`. `native` is the host. Cross compilation is the normal case: the
default C toolchain carries the C library headers of every target it supports
([../compiler/driver-toolchain-sdk.md](../compiler/driver-toolchain-sdk.md)).

## Conditional compilation

Comptime `if` on `Builtin` is the default way to vary code by target and mode
([comptime.md](comptime.md#platform-code)). When a branch would not type-check on every target,
`#if` uses the symbols the build defines, and only those (`CD1023`):

| Symbol | Defined when |
|---|---|
| `OS_LINUX`, `OS_WINDOWS`, `OS_MACOS`, `OS_FREEBSD`, `OS_WASI`, `OS_FREESTANDING` | the target OS |
| `ARCH_X86_64`, `ARCH_AARCH64`, `ARCH_X86`, `ARCH_ARM`, `ARCH_RISCV64`, `ARCH_WASM32` | the target architecture |
| `ABI_GNU`, `ABI_MUSL`, `ABI_MSVC` | the target ABI |
| `MODE_DEBUG`, `MODE_RELEASE_SAFE`, `MODE_RELEASE_FAST`, `MODE_RELEASE_SMALL` | the build mode |
| `LINK_LIBC` | libc is linked |
| `TEST` | building tests |

A source file named `*.<os>.cs` (for example `Fs.windows.cs`) is compiled only for that OS.

## Tests

```csharp
static class ParseTests
{
    [Test]
    static Result DecimalDigits()
    {
        Try(Testing.ExpectEqual(42u, Try(Parse.Decimal("42"))));
        return default;
    }
}
```

- A test is a `static` method with `[Test]`, no parameters, returning `void` or `Result`.
- `cdc test` (and `dotnet test` through the SDK) builds the package with `Builtin.IsTest` and the
  `TEST` symbol, generates a `Main` that runs every test, and reports each one. A test fails by
  returning an error or by panicking; a panic fails that test only, because each test runs in its
  own process when the OS has processes, and the runner stops at the first panic otherwise.
- `Testing.Allocator` is a `DebugAllocator` per test; a leak fails the test.
- Tests are not compiled into non-test builds.
- `cdc test --filter <text>` runs tests whose full name contains the text.

## Generated sources

Code that comptime cannot express because it declares new types is generated before compilation
by an MSBuild target or by any tool, written to `obj/` and added to `Compile`. The generator may be
a CΔ program of the same repository, run by the build. This is the only code generation; there is
no macro system.
