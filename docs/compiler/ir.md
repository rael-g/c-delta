# Delta IR

Delta IR is the only thing the core sees and the only thing a backend receives. It must carry a
whole CΔ program, the declarations of any C header, and later other source languages, so it is
defined by what systems languages share, not by C# or by C.

## Design choices

| Choice | Decision | Reason |
|---|---|---|
| Form | typed control-flow graph of basic blocks over named local slots, **not SSA** | maps directly to C, is trivial to interpret at comptime, and keeps every source variable visible for debugging; SSA would be rebuilt by the C compiler anyway |
| Calls | ordinary statements, not terminators | there is no unwinding |
| Memory | explicit places: locals, globals, fields, dereferences, elements | the C backend writes them as lvalues; the interpreter as addresses |
| Generics | type parameters and comptime parameters exist in the IR until instantiation | the frontend lowers each generic body once |
| Safety | explicit `check` statements, inserted by the core | the build mode is a core decision, not a frontend or backend one |
| Layout | computed by the core, stored on types | backends and the interpreter read it; nobody recomputes it |
| Attributes | open list of named, typed constant arguments on every declaration | carries C# attributes, C doc tags and projection hints without changing the model |
| Serialization | one textual format, printed and parsed, round-trip exact; a binary format from M10 | text for golden tests, dumps, and tests of the core and backends without a frontend; binary for the call from the host to the native core |

## Stages

One model, three stages. The verifier knows which constructs each stage allows.

| Stage | Produced by | Contains | Does not contain |
|---|---|---|---|
| `Lowered` | frontends | type parameters, comptime parameters, `default` arithmetic mode, constrained calls, comptime regions, `Try` already expanded, defers already placed | layouts, error codes, safety checks |
| `Instantiated` | core | concrete types only, comptime results as constants, resolved calls, vtables and union layouts | type parameters, comptime regions, constrained calls |
| `Final` | core | layouts, error codes, explicit `check` statements, resolved arithmetic modes, only reachable declarations | `default` mode |

## Module

```
Module
  Name
  Stage
  Target            (Final only: triple, data layout)
  SourceFiles       id -> path, for spans
  CImports          header sets: headers, defines, include dirs; referenced by declarations
  Types             struct, enum, union, interface and opaque declarations
  Globals
  Functions
  Impls             (struct, interface) -> method table
  ErrorNames        Final only: name -> code
  Attributes        on the module
```

Every declaration has a `SymbolId`, a `Name` (a path such as `Std.Mem.Copy`), a `Span`, a list
of `Attributes`, and an `Origin`:

| Origin | Meaning |
|---|---|
| `Source(language)` | defined by a frontend from source; the backend emits its definition |
| `CHeader(importId, cName)` | declared by a C header; the backend includes the header and never redefines it |
| `CLibrary(library, cName)` | declared by hand for a C library (`[DllImport]`); the backend writes a prototype |
| `Synthetic` | created by the core (thunks, vtables, the test runner, `main`) |

## Types

| Type | Notes |
|---|---|
| `void`, `noreturn` | |
| `bool` | |
| `i8 i16 i32 i64 i128`, `u8 u16 u32 u64 u128` | |
| `isize`, `usize` | pointer-sized |
| `f16 f32 f64` | |
| `c.char c.schar c.uchar c.short c.ushort c.int c.uint c.long c.ulong c.longlong c.ulonglong` | C types whose spelling matters to the backend; sizes come from the target |
| `ptr<T>` with flags `nullable`, `const`, `volatile`, `many` | `ref T` is `ptr<T>` not nullable, not many; `T*` is nullable and many |
| `slice<T>` with flag `const` | pointer and `i32` length |
| `array<T, N>` | value array |
| `fn(sig)` | function pointer; `sig` = calling convention, parameters, return, variadic |
| `closure(sig)` | function pointer and context pointer |
| `dyn<I>` | self pointer and table pointer |
| `optional<T>` | |
| `result<T, E>` | `E` is an error set or `anyerror`; `T` may be `void` |
| `errorset{names}`, `anyerror` | |
| `struct S<args>`, `enum E`, `union U`, `opaque O` | references to declarations |
| `param T` | a type parameter; `Lowered` only |
| `comptime.type`, `comptime.typeinfo` | comptime-only values |

Parameter passing modes are part of the signature: `value`, `ref`, `ref.readonly`, `out`. They
lower to pointers in the backend; the core and the interpreter keep the distinction for
diagnostics and for `out` definite assignment.

### Declarations of types

```
Struct   name, type params, fields (name, type, attributes, [Bits n]),
         layout: c | c.packed(n) | auto | explicit(offsets), min size,
         computed: size, alignment, field offsets (Final)
Enum     name, backing integer type, members (name, value), exhaustive flag
Union    name, members (struct types), computed: tag type, payload size, alignment, member tags
Interface name, type params, methods (signature, has default body), static members,
         extends
Opaque   name
```

Untagged C unions are structs with explicit layout and every offset `0`.

## Globals

```
Global   name, type, mutable, thread-local, initializer (constant | comptime function | none),
         linkage (internal | export(name) | extern), section, alignment
```

An `extern` global has no initializer and is defined elsewhere (C or the linker). Every other
global has a constant initializer in `Instantiated` and later.

## Functions

```
Function  name, type params, comptime params, params (name, type, mode), return type,
          calling convention, linkage (internal | export(name, weak?) | extern | c.macro),
          inline (default | always | never), safety override, attributes,
          body (optional)
Body      locals, blocks, entry block, scopes
Local     id, name (optional), type, kind (param | user | temp), span
Block     id, statements, terminator
```

### Places, operands and values

```
Place    := local | global | Place.field(i) | *Operand | Place[Operand] | Place.payload(variant)
Operand  := copy Place | const Constant | fnref Function<args> | undef Type
Rvalue   := Operand
          | binop(op, mode, Operand, Operand)
          | unop(op, mode, Operand)
          | cast(kind, mode, Operand, Type)
          | addr(Place, mutable?)
          | aggregate(Type, Operand...)
          | optional.some(Operand) | optional.none(Type)
          | result.ok(Type, Operand) | result.err(Type, Operand)
          | union.make(Type, member, Operand)
          | closure.make(Function, Operand context)
          | dyn.make(Impl, Operand self)
          | len(Place) | tag(Place) | haspayload(Place) | errcode(Operand)
          | sizeof(Type) | alignof(Type) | offsetof(Type, field)
          | call(Callee, Operand...)
          | intrinsic(name, Operand...)
          | comptime(Function)                 Lowered only
```

- `op`: `add sub mul div rem shl shr and or xor eq ne lt le gt ge neg not bitnot`, typed by
  their operands. Float and integer variants are distinct.
- `mode` for integer `add sub mul neg` and conversions: `default` (Lowered and Instantiated only),
  `checked` (always trap), `wrap`, `assume` (illegal behavior assumed impossible). The core
  rewrites `default` to `trap` or `assume` by build mode and safety overrides.
- `cast kind`: `int.int`, `int.float`, `float.int`, `float.float`, `int.enum`, `enum.int`,
  `ptr.int`, `int.ptr`, `ptr.ptr`, `bitcast`, `array.slice`, `slice.ptr`.
- `Callee`: `direct(Function<args>)`, `indirect(Operand)` for function pointers,
  `closure(Operand)`, `dyn(Operand, method)`, `constrained(param T, Interface.method)` (Lowered
  only, resolved by instantiation), `c.macro(name)`.
- Constants: integers (arbitrary precision with a type), floats (bit pattern), `bool`, `null`,
  bytes (static data, with or without NUL), aggregates, pointers to globals with an offset,
  function references, error codes, `undef`.

### Statements and terminators

```
Statement   := Place = Rvalue
             | call(Callee, Operand...)                 result discarded
             | check(kind, Operand condition, message)  Final only
             | live(local) | dead(local)                scope of a local's storage
             | fill.undef(Place)                        Debug 0xAA fill
             | trace.push(span)                         error return trace
Terminator  := goto Block
             | branch Operand ? Block : Block
             | switch Operand [constant -> Block]... default Block
             | return Operand?
             | unreachable(mode)                        trap or assume
             | panic(Operand message)
```

Every statement and terminator has a span. `check` kinds are listed in
[core-passes.md](core-passes.md#7-safety).

`Defer`, `ErrDefer`, `using` and `Try` do not exist in the IR: the frontend copies deferred bodies
onto every exit edge of their scope and expands `Try` into a test, the error-path deferred
bodies, and a `return`. Each backend and the interpreter therefore never deal with them.

## Impls and dispatch

```
Impl     struct S<args>, interface I<args>, method -> Function
```

`dyn.make(Impl, self)` takes the table of an `Impl`. The core emits one table global per `Impl`
used by a `dyn`. `[Union]` interfaces do not use tables: a call on a union value is lowered by the
core into a `switch` on the tag.

## Attributes

```
Attribute  name (namespaced: cs.Obsolete, cdelta.Export, c.tag.out, proj.cs.name), arguments
           (positional and named constants)
```

The IR never interprets an attribute it does not own; the namespace says who does. This is how
kabic's tags (`out`, `array_of`, `owner`, `opt`, `borrowed`) travel from the C frontend to the
projection backends without new IR constructs ([../kabic-migration.md](../kabic-migration.md)).

## Text format

```
module Hello stage final target x86_64-linux-gnu

source 1 "src/Program.cs"

struct Hello.Vec2 layout c size 8 align 4 {
  X: f32 @0
  Y: f32 @4
}

fn Hello.Vec2.Dot(this: ptr<struct Hello.Vec2> const, other: struct Hello.Vec2) -> f32 {
  local %0 this: ptr<struct Hello.Vec2> const param
  local %1 other: struct Hello.Vec2 param
  local %2: f32 temp
  local %3: f32 temp
  local %4: f32 temp
bb0:
  %2 = binop fmul copy (*%0).field(0), copy %1.field(0)  @1:7:47
  %3 = binop fmul copy (*%0).field(1), copy %1.field(1)  @1:7:47
  %4 = binop fadd copy %2, copy %3                       @1:7:47
  return copy %4                                         @1:7:47
}
```

The grammar is defined by the printer and parser in `CDelta.Ir`, and the invariant
`Parse(Print(m))` equals `m` is tested on every golden file.

## Verifier

The verifier checks, at the stage the module claims:

- every reference resolves (types, functions, globals, locals, blocks);
- every operand and place is typed consistently with its use;
- every block ends in exactly one terminator and is reachable from the entry, or is removed;
- no construct appears outside the stages that allow it;
- every local is assigned before use on every path (`undef` counts as assigned);
- `Final`: every struct has a layout, every `dyn` has a table, every error has a code, no
  `default` mode remains, and every extern declaration has an origin;
- spans exist on every statement.

A failure is always a compiler bug (`CD9002`).

## API

- `ModuleBuilder`, `FunctionBuilder` (block cursor, local allocation, emission helpers).
- `IrPrinter`, `IrParser`.
- `IrVerifier.Verify(module, stage)`.
- `IBodyProvider`: implemented by frontends; `Body? Lower(Function)` is how the core asks for the
  generic body of a function the first time an instantiation reaches it. The core substitutes type
  and comptime arguments itself, so a frontend lowers each body once.
- `Target`: triple, pointer size, endianness, C type sizes and alignments, and the C ABI rules for
  struct layout.
