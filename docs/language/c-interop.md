# C interoperability

CΔ reads C headers directly, compiles C sources with the program, calls C at zero cost, and exports
CΔ code with the C ABI and a generated header.

## Importing a header

```csharp
[Import("sqlite3.h")]
static unsafe partial class Sqlite { }

[Import("stdio.h", "stdlib.h", Defines = ["_GNU_SOURCE"])]
static unsafe partial class C { }
```

`[Import]` takes one or more headers and these named arguments:

| Argument | Meaning |
|---|---|
| `Defines` | macros defined before the headers, `NAME` or `NAME=value` |
| `IncludeDirs` | extra include directories, relative to the project |
| `Only` | C name prefixes to keep; everything else in the headers is skipped |

Include directories, defines and the target that apply to every import come from the project
([modules-build.md](modules-build.md)).

How it works ([../compiler/frontend-c.md](../compiler/frontend-c.md)):

1. Before Roslyn runs, the C frontend parses the headers with libclang for the build target, as a
   translation unit containing the `#include` lines and defines above.
2. It generates a C# source file with the declarations as members of the partial class, with C
   names unchanged. Roslyn compiles it with the program, and the IDE sees it through the SDK.
3. It records each declaration in Delta IR with its exact C type and layout.
4. The C backend writes the same `#include` lines into the generated C, so calls go through the
   real header, including `static inline` functions and macros that expand to function calls.

Two `[Import]` classes that include the same header share one set of C declarations; each class
exposes them under its own name.

## What a header becomes

| C | CΔ |
|---|---|
| function | `public static extern` method, `[CName]` when renamed |
| variadic function | `public static extern` method ending in `__arglist`; call as `C.printf("%d\n", __arglist(x))` |
| `static inline` function | the same as a function; the C compiler compiles the body from the header |
| global variable | `public static` field with `[CName]`; the C backend references the C symbol and never defines it |
| struct | `public struct`, sequential layout; `[StructLayout(Pack = n)]` when the header packs it |
| union | `public struct` with `LayoutKind.Explicit` and every field at `[FieldOffset(0)]` |
| incomplete struct | `[Opaque] public struct` |
| bit-field | field with `[Bits(n)]` |
| anonymous struct or union member | nested struct named `<parent>_<n>` where `n` counts anonymous members from `0` |
| array field `T x[N]` | `fixed` buffer for a primitive `T`, otherwise a generated `[InlineArray(N)]` struct |
| flexible array member | omitted; the struct gets `[CFlexible("name", typeof(T))]` and the std helper `Std.C.Flexible<T>` reaches the elements |
| enum | `public enum` with the underlying type clang reports, non-exhaustive; constants with their values |
| `typedef` of a struct, union or enum | the record takes the typedef name; the tag name, if different, is recorded in `[CName]` |
| other `typedef` | replaced by its underlying type everywhere |
| object-like macro evaluating to an integer, float, character or string constant | `public const` with the C type of the expression |
| object-like macro evaluating to a constant address: an integer constant cast to a pointer, possibly offset | `[CMacro] public static readonly` field of the pointer type; reading it emits the macro, and writing it is a compile error as for any `readonly` field |
| other object-like macro | skipped |
| function-like macro | skipped; declare it with `[CMacro]` |
| anything that cannot be represented | declared with `[Unsupported("reason")]`; using it is `CD6003` |

`CD6003` covers, among others: `long double`, `_Complex`, `_Atomic`, vector types, K&R
declarations, functions taking or returning a struct by value with a flexible array member.

### Type mapping

| C | CΔ |
|---|---|
| `char` | `byte` |
| `signed char`, `unsigned char` | `sbyte`, `byte` |
| `short`, `int`, `long long` and unsigned forms | `short`, `int`, `long` and unsigned forms |
| `long`, `unsigned long` | `CLong`, `CULong` |
| `int8_t` ... `uint64_t` | `sbyte` ... `ulong` |
| `size_t`, `uintptr_t` | `nuint` |
| `ptrdiff_t`, `intptr_t`, `ssize_t` | `nint` |
| `_Bool`, `bool` | `bool` |
| `float`, `double` | `float`, `double` |
| `wchar_t`, `char16_t`, `char32_t` | the integer of the target's size and signedness |
| `T*`, `const T*` | `T*`; constness is kept in Delta IR and in the generated C |
| `void*` | `void*` |
| `T[N]` parameter | `T*` |
| function pointer | `delegate*<...>` for the target's default convention; `delegate* unmanaged[Stdcall]<...>` and the other named conventions only when the header states a different one |

Imports are per target: a header read for `x86_64-windows` and for `x86_64-linux` may produce
different declarations. Code written against `CLong`, `nuint` and the declared names stays
portable.

### Macros

A macro that expands to declarations (X-macros, tables of functions or structs instantiated by a
macro) needs nothing: the header is read after preprocessing, so what the expansion declares is
imported like any other declaration. To use such a library, instantiate its macros in a header of
the project and import that header.

A function-like macro used as a call is declared by hand with its intended signature (D030):

```csharp
static unsafe partial class C
{
    [CMacro] public static extern int isdigit(int c);
}
```

The C backend writes a call of the macro with the arguments; the C compiler expands it and checks
the expansion. `[CMacro]` applies only to `static extern` methods (`CD6008`). Every form of macro
has a declaration (D030):

| Macro | Declaration | Written in C as |
|---|---|---|
| an expression | `[CMacro] static extern int isdigit(int c);` | `isdigit(c)` |
| a statement, such as `do { ... } while (0)` | a `void` method | `NAME(args);` as a statement |
| one that assigns an argument | that parameter is `ref` | the lvalue itself, `(*p)`, not its address |
| generic in its arguments (`MAX`, `_Generic`, containers) | a generic method, instantiated like any other | the same call in each instance, with that instance's C types |
| one that takes a type, or is not shaped like a call | `[CMacro("template")]` | the template, with `{0}`, `{1}` ... replaced by the arguments and `{T}` by the C spelling of type argument `T` |

```csharp
static unsafe partial class Ds
{
    [CMacro] public static extern void arrput<T>(ref T* a, T v);
    [CMacro] public static extern nint arrlen<T>(T* a);
}

static unsafe partial class C
{
    [CMacro("va_arg({0}, {T})")] public static extern T va_arg<T>(ref va_list ap);
    [CMacro("offsetof({T}, {0})")] public static extern nuint offsetof<T>([Comptime] string field);
}
```

A template that names an argument or a type argument the method does not have is `CD6011`. An
argument of a `[Comptime]` `string` parameter is written as its text, unquoted, which is how a
macro receives a field or a member name.

## Declaring a C function by hand

`[DllImport]` declares one C function without a header:

```csharp
static unsafe class LibC
{
    [DllImport("c")] public static extern nint write(int fd, byte* buf, nuint count);
}
```

- The library name becomes a link input (`-lc`). Static or dynamic linking is the build's choice
  ([modules-build.md](modules-build.md)); nothing is loaded by name at run time.
- `EntryPoint` gives the C name; `CallingConvention` gives the convention. `CharSet`,
  `SetLastError`, `PreserveSig = false`, `BestFitMapping`, `ThrowOnUnmappableChar` and marshaling
  attributes are rejected (`CD6007`): there is no marshaling, the signature is the ABI.
- The C backend declares the prototype itself, since there is no header, unless an imported
  header already declares that name; then the header's declaration is used.
- Two `[DllImport]` declarations of the same C function must have the same signature (`CD6010`).

## Compiling C sources

C sources listed in the project ([modules-build.md](modules-build.md)) are compiled by the same C
toolchain, with the same target and mode, and linked into the output. A program can mix CΔ and C
freely; C calls CΔ through `[Export]`.

## Exporting

```csharp
[Export]
public struct Point { public int X; public int Y; }

static class Geometry
{
    [Export("geo_distance2")]
    public static int Distance2(Point a, Point b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
```

- `[Export]` gives a symbol the C ABI and its name, unmangled: the declared name, or the one given.
- Exportable: `public static` non-generic methods, structs with sequential or explicit layout,
  enums, `public static` fields.
- Exported signatures use only C types: primitives except `char`, `Int128`, `UInt128` and `Half`,
  pointers, function pointers, `CLong`, `CULong`, exported structs and enums, and `void`
  (`CD6004`). Spans, `Result`, optionals, delegates and interface values must be lowered to C
  types by the exporter: a `Span<T>` becomes a pointer and a length.
- Generic and variadic methods cannot be exported (`CD6006`). Two exports with one name are
  `CD6005`.
- `[Linkage(LinkageKind.Weak)]` makes the symbol weak; `[Section("name")]` places it in a section.

### Generated header

Every build that has exports writes `<PackageName>.h` next to the output, in `include/` for a
library ([../compiler/driver-toolchain-sdk.md](../compiler/driver-toolchain-sdk.md#outputs-and-intermediate-files)):

- an include guard, `<stdint.h>`, `<stddef.h>` and `<stdbool.h>`, and an `extern "C"` block for
  C++;
- exported enums, then structs in dependency order, then fields, then function prototypes;
- the doc comments of exported declarations, as C comments;
- `_Static_assert` on the size of every exported struct.

### Output kinds

| Output | What |
|---|---|
| executable | needs `Main` (`CD0007`) |
| static library | `.a` or `.lib` with the exports and the header, plus `<PackageName>Config.cmake` and `<PackageName>.pc` so CMake's `find_package` and `pkg-config` find it, laid out as an installed prefix |
| shared library | `.so`, `.dll` or `.dylib`, versioned by `Version` with the major version in its soname; only exports are visible, everything else has hidden visibility; with the same CMake and `pkg-config` files |
| object | one `.o` or `.obj` |
| C source | the generated `.c` files, the header and a `CMakeLists.txt` defining the library target, to vendor into a C project or reach with CMake's `FetchContent`; building it needs a C compiler and nothing of CΔ; see [../compiler/backend-c.md](../compiler/backend-c.md) |

### Bindings for other languages

The exported surface is a Delta IR module of declarations. Projections turn it into
idiomatic bindings for other languages, starting with C# and Zig, which is the role kabic has
today ([../kabic-migration.md](../kabic-migration.md)).
