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

Fields are always `public` (`CD1025`): there is no encapsulation of data. A struct that wants a
narrow surface exposes functions and documents which fields are its contract.

## Packages

A package is one project file and the sources under its directory. It builds to an executable, a
library, or nothing (a source-only library consumed by others).

Compilation is whole-program: a build compiles the sources of the root package and of every
package it depends on, transitively, in one run of the compiler. There is no binary package
format for CΔ code, so generics, comptime and inlining cross package boundaries with no
restriction. Each package is still bound by Roslyn as its own compilation, so `internal` holds
between packages.

Dependencies are ordinary MSBuild references:

| Dependency | How |
|---|---|
| a package in the same repository | `<ProjectReference Include="../Lib/Lib.csproj" />` |
| a published package | `<PackageReference Include="Name" Version="1.2.3" />`, restored from NuGet |

A published CΔ package carries its sources, its C headers and C sources, and a Roslyn reference
assembly for the IDE. The format is in [../compiler/driver-toolchain-sdk.md](../compiler/driver-toolchain-sdk.md).

## The project file

```xml
<Project Sdk="CDelta.Sdk/0.1.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <Target>x86_64-linux-gnu</Target>
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
| `OutputType` | `Exe`, `Library`, `None` | `Exe` |
| `LibraryKind` | `Static`, `Shared` | `Static` |
| `Target` | a target triple, or `native` | `native` |
| `OptimizeMode` | `Debug`, `ReleaseSafe`, `ReleaseFast`, `ReleaseSmall` | `Debug` for `Configuration=Debug`, `ReleaseSafe` for `Release` |
| `LinkLibC` | `true`, `false` | `true` unless the target OS is `freestanding` |
| `CToolchain` | `zig`, `clang`, `gcc`, `msvc`, `auto` | `auto` |
| `EmitC` | `true` to keep the generated C next to the output | `false` |
| `PackageName` | the C-visible name of the package (header name, library name) | the project name |
| `ErrorReturnTrace` | `true`, `false` | `true` in safe modes |
| `DebugSymbols` | `true`, `false` | `true` in Debug and ReleaseSafe |

### Items

| Item | Metadata | Meaning |
|---|---|---|
| `Compile` | | CΔ sources; by default every `*.cs` under the project directory |
| `CSource` | `Flags` | C sources compiled and linked |
| `CIncludeDir` | | include directories for imports and C sources |
| `CDefine` | `Value` | macros for imports and C sources |
| `NativeLibrary` | `Linkage` (`Static`, `Dynamic`) | libraries to link |
| `NativeLibraryDir` | | library search directories |
| `BuildOption` | `Value`, `Type` | a value read by `Builtin.Option<T>(name)` |

## Targets

A target is `<arch>-<os>-<abi>`: `x86_64-linux-gnu`, `x86_64-linux-musl`, `aarch64-linux-gnu`,
`x86_64-windows-gnu`, `x86_64-windows-msvc`, `aarch64-macos-none`, `wasm32-wasi-musl`,
`x86_64-freestanding-none`. `native` is the host. Cross compilation is the normal case: the
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
