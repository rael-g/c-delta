# C frontend

The C frontend reads C with libclang and produces Delta IR declarations with exact C types,
layouts, constants and doc tags. It is the same component kabic's reader becomes
([../kabic-migration.md](../kabic-migration.md)).

## libclang

- Access through ClangSharp's managed API over libclang, with the libclang runtime packages pinned
  to one version for every host platform.
- libclang parses; it never compiles. Code generation of C always goes through the C toolchain.
- Clang is asked for everything it knows: types, record sizes, alignments, field offsets,
  bit-field widths, enum underlying types, calling conventions, evaluated constants and comments.
  The frontend reimplements none of it.

## The translation unit

For an import set, the frontend writes a synthetic source:

```c
#define _GNU_SOURCE
#include "stdio.h"
#include "stdlib.h"
```

and parses it with:

| Argument | From |
|---|---|
| `-target <triple>` | the build target |
| `-std=c11` | fixed |
| `-isystem ...` | the toolchain's C library headers for the target |
| `-I ...` | `CIncludeDir` items and the import's `IncludeDirs` |
| `-D ...` | `CDefine` items and the import's `Defines` |
| `-fparse-all-comments` | so documentation comments are available |
| detailed preprocessing record | so macros are available |

Clang errors become `CD6001` at the header location; clang warnings are ignored.

All import sets of one program must agree: they are included into one generated C translation
unit, so two imports that define the same macro differently are `CD6009`.

## Declarations

Every declaration in the translation unit is read, including those of headers included
transitively, because that is what including a header makes visible in C. `Only` filters by name
prefix. Declarations are identified by their clang USR, so a struct reached by two imports is one
IR declaration, and two `[Import]` classes share it.

| Cursor | IR |
|---|---|
| function declaration | `Function` with no body, origin `CHeader`, calling convention from clang |
| variable with external linkage | `Global` with linkage `extern` |
| struct, union | `Struct` with layout `c` or `explicit`, sizes and offsets from clang |
| incomplete record | `Opaque` |
| enum | `Enum`, not exhaustive, underlying type from clang |
| typedef | resolved as described in [../language/c-interop.md](../language/c-interop.md#what-a-header-becomes); the typedef name is kept as `c.typedef` attribute for the backend spelling |
| object-like macro | evaluated, below |
| anything not representable | a declaration with `cdelta.Unsupported(reason)` |

## Macros

For each object-like macro, the frontend appends a declaration to a second parse of the same
translation unit:

```c
static const __typeof__(NAME) __cdelta_macro_NAME = (NAME);
```

and evaluates it with clang's constant evaluation. If it parses and evaluates to an integer,
float, character or string, the macro becomes a constant with that type. Otherwise it is skipped.
Function-like macros are never imported automatically; `[CMacro]` declares them by hand.

## Doc tags and annotations

Doc comments are read with their tags: a tag states a fact about a declaration that C cannot
express (a pointer written back, a pointer and a count forming one sequence, a borrowed or
optional value). Tags become annotations of the IR ([ir.md](ir.md#annotations)). The CΔ import
ignores them; projections use them.

## Import stubs

`CDelta.Projection.CSharp` turns the IR declarations of an import into the C# source Roslyn compiles:

```csharp
static unsafe partial class Sqlite
{
    [CName("sqlite3")] [Opaque] public struct sqlite3 { }

    public const int SQLITE_OK = 0;

    public static extern int sqlite3_open(byte* filename, sqlite3** ppDb);

    public static extern int sqlite3_close(sqlite3* db);
}
```

- C names are kept; a name that is a C# keyword is written with `@`.
- The file is generated into `obj/cdelta/imports/` with a header comment marking it generated, and
  doc comments copied from the C declarations so the IDE shows them.
- A declaration shared by two import classes is generated in each, and both map to one IR
  declaration through `[CName]` and the import identity.

## Function bodies

Translating C function bodies into IR is a later milestone. It enables comptime evaluation of
`static inline` C functions and backends other than C. The design constraint holds already: C
semantics are expressible in the IR without new constructs (signed overflow is `assume`, unsigned
arithmetic is `wrap`, integer promotions are explicit casts, evaluation order is the one clang's
AST sequences).

## Caching

The import cache key is the hash of every file clang opened, the defines, the include directories,
the target and the libclang version. A hit skips parsing and reuses the IR and the stubs.
