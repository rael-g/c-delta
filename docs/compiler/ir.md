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
  SourceFiles       id -> full path and display path, for spans
  CImports          header sets: headers, defines, include dirs; referenced by declarations
  Types             struct, enum, union, interface and opaque declarations
  Globals
  Functions
  Impls             (struct, interface) -> method table
  ErrorNames        Final only: name -> code
  Attributes        on the module
```

Every declaration has an id (its index in its table), a `Key`, a `Name`, a `Span`, a list of
`Attributes`, and an `Origin`.

- `Key` is unique among the declarations of its kind in the module and is how the text format and
  every lookup refer to it. The C# frontend uses the documentation comment ID without its kind
  prefix (`Hello.Program.Count(System.Int32)`, ``Std.List`1``), the C frontend the C name prefixed
  with `c:` (`c:fopen`), and the core `synthetic:` followed by a description (`synthetic:main`).
- `Name` is the path used for display and for C names (`Hello.Program.Count`). Two functions may
  share a `Name`; only `Key` is unique.

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
Body      locals, blocks; the entry is the first block
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

- `op` on integers: `add sub mul div rem shl shr and or xor eq ne lt le gt ge neg not bitnot`;
  signedness comes from the operand type. On floats: `fadd fsub fmul fdiv frem fneg feq fne flt
  fle fgt fge`. `not` is on `bool` only.
- `mode` for integer `add sub mul neg` and the casts `int.int`, `float.int` and `int.enum`:
  `default` (Lowered and Instantiated only), `trap` (always checked; what a C# `checked` context
  gives), `wrap` (what `unchecked` gives; saturating for `float.int`), `assume` (illegal behavior
  assumed impossible). The core rewrites `default` to `trap` or `assume` by build mode and safety
  overrides. Other operations have no mode.
- `cast kind`: `int.int`, `int.float`, `float.int`, `float.float`, `int.enum`, `enum.int`,
  `ptr.int`, `int.ptr`, `ptr.ptr`, `bitcast`, `array.slice`, `slice.ptr`.
- `Callee`: `direct(Function<args>)`, `indirect(Operand)` for function pointers,
  `closure(Operand)`, `dyn(Operand, method)`, `constrained(param T, Interface.method)` (Lowered
  only, resolved by instantiation), `c.macro(name)`.
- Constants: integers (128 bits of two's complement with a type, the widest IR integer), floats (bit pattern), `bool`, `null`,
  bytes (static data, with or without NUL), aggregates, pointers to globals with an offset,
  function references, error codes, `undef`.

### Statements and terminators

```
Statement   := Place = Rvalue
             | call(Callee, Operand...)                 result discarded
             | check(kind, Operand condition, message)  Final only
             | live(local) | dead(local)                scope of a local's storage
             | fill.undef(Place)                        Debug 0xAA fill
             | trace.push                               record the statement's span in the error return trace
             | trace.save(local) | trace.restore(local)  save and restore the trace length around handled errors
Terminator  := goto Block
             | branch Operand ? Block : Block
             | switch Operand [constant -> Block]... default Block
             | return Operand?
             | unreachable(mode)                        default (Lowered and Instantiated), trap or assume
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

source 1 "$ROOT/samples/hello/src/Program.cs" "src/Program.cs"

struct Hello.Vec2 layout c size 8 align 4 @1:3:8 {
  X: f32 @0
  Y: f32 @4
}

fn "Hello.Vec2.Dot(Hello.Vec2)"(this: ptr<struct Hello.Vec2> const, other: struct Hello.Vec2) -> f32 @1:7:27 {
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

The invariant `Parse(Print(m))` equals `m` is tested on every golden file, and
`Print(Parse(text))` equals `text` for every text the printer produced.

### Lexical rules

- One declaration header, field, local, statement or terminator per line. `//` starts a comment
  to the end of the line; the printer writes none.
- Indentation is two spaces per level; the parser ignores whitespace other than line ends.
- A **reference** (a key) is written bare when it matches
  `[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*` and is not a keyword of this grammar,
  otherwise as a string.
- A **string** is `"..."` with the escapes `\"`, `\\`, `\n`, `\t` and `\xHH`; every byte
  outside printable ASCII is written `\xHH`. Strings hold UTF-8 bytes.
- Integers are decimal with an optional `-`; float constants are bit patterns in hexadecimal.
- A **span** is `@file:line:column` and ends the line it belongs to.
- Locals are `%n`, blocks `bbn`, both numbered from `0` by their index.

### Grammar

```
module      = "module" ref "stage" stage ["target" triple] NL {mattr} {item}
stage       = "lowered" | "instantiated" | "final"
mattr       = "#![" attr "]" NL
attrs       = {"#[" attr "]" NL}
attr        = ref ["(" [aarg {"," aarg}] ")"]
aarg        = [ident "="] value

item        = source | cimport | struct | enum | union | interface | opaque | global
            | function | impl | errorname
source      = "source" int string string NL
cimport     = "cimport" int "{" NL {("header" | "define" | "include") string NL} "}" NL
origin      = "origin" ( "source" "(" ident ")" | "cheader" "(" int "," string ")"
                       | "clibrary" "(" string "," string ")" | "synthetic" )

struct      = attrs "struct" ref [tparams] [layout] ["size" int "align" int] ["minsize" int]
              [origin] span "{" NL {field} "}" NL
layout      = "layout" ("c" | "c.packed" "(" int ")" | "auto" | "explicit")
field       = attrs ident ":" type ["bits" int] ["@" int] NL
enum        = attrs "enum" ref ":" type ["exhaustive"] [origin] span "{" NL {ident "=" int NL} "}" NL
union       = attrs "union" ref ["tag" type "size" int "align" int] span "{" NL {type ["=" int] NL} "}" NL
interface   = attrs "interface" ref [tparams] ["extends" type {"," type}] span "{" NL {imethod} "}" NL
imethod     = ["static"] "fn" ident sig ["default" ref] NL
opaque      = attrs "opaque" ref [origin] span NL
global      = attrs "global" ref ":" type {gflag} ["=" (constant | "comptime" ref)] [origin] span NL
gflag       = "mutable" | "threadlocal" | linkage | "section" string | "align" int
function    = attrs "fn" ref [tparams] "(" [param {"," param}] ["," "..."] ")" "->" type
              {fflag} [origin] span (body | NL)
param       = ident ":" [pmode] type
pmode       = "ref" | "ref.readonly" | "out"
fflag       = "cc" conv | linkage | "inline" ("always" | "never") | "safety" ("on" | "off")
linkage     = "internal" | "export" "(" string ")" ["weak"] | "extern" | "c.macro"
conv        = "default" | "cdecl" | "stdcall" | "fastcall" | "thiscall" | "vectorcall"
impl        = "impl" ref "=" type ":" type "{" NL {ident "=" ref [targs] NL} "}" NL
errorname   = "error" ident "=" int NL

tparams     = "<" tparam {"," tparam} ">"
tparam      = "type" ident [":" constraint {"," constraint}] | "comptime" ident ":" type
constraint  = type | "struct" | "unmanaged" | "new" | "refstruct"
targs       = "<" targ {"," targ} ">"
targ        = type | constant

type        = "void" | "noreturn" | "bool" | int-type | "isize" | "usize" | "f16" | "f32" | "f64"
            | "c." ident
            | "ptr" "<" type ">" {"nullable" | "const" | "volatile" | "many"}
            | "slice" "<" type ">" ["const"]
            | "array" "<" type "," int ">"
            | "fn" sig | "closure" sig
            | "dyn" "<" type ">"
            | "optional" "<" type ">"
            | "result" "<" type "," type ">"
            | "errorset" "{" ident {"," ident} "}" | "anyerror"
            | ("struct" | "interface") ref [targs] | ("enum" | "union" | "opaque") ref
            | "param" ident | "comptime.type" | "comptime.typeinfo"
int-type    = "i8" | "i16" | "i32" | "i64" | "i128" | "u8" | "u16" | "u32" | "u64" | "u128"
sig         = "(" [[pmode] type {"," [pmode] type}] ["," "..."] ")" "->" type ["cc" conv]

body        = "{" NL {local} block {block} "}" NL
local       = "local" lref [ident] ":" type ("param" | "user" | "temp") [span] NL
block       = bref ":" NL {stmt} term
stmt        = ( place "=" rvalue
              | "call" callee "(" [operand {"," operand}] ")"
              | "check" ckind operand string
              | "live" lref | "dead" lref
              | "fill.undef" place
              | "trace.push" | "trace.save" lref | "trace.restore" lref ) span NL
term        = ( "goto" bref
              | "branch" operand "?" bref ":" bref
              | "switch" operand "[" {constant "->" bref} "]" "default" bref
              | "return" [operand]
              | "unreachable" ("default" | "trap" | "assume")
              | "panic" operand ) span NL
ckind       = "bounds" | "null" | "optional" | "result.value" | "result.error" | "enum"
            | "align" | "divzero" | "overlap"

place       = base {"." "field" "(" int ")" | "[" operand "]" | "." "payload" "(" int ")"}
base        = lref | "global" ref | "(" "*" lref ")"
operand     = "copy" place | "const" constant | "fnref" ref [targs] | "undef" type
constant    = type value
value       = int | "bits" hex | "true" | "false" | "null" | "undef" | "bytes" string
            | "{" [value {"," value}] "}" | "&" ref ["+" int] | "fn" ref [targs] | "error" ident
rvalue      = operand
            | "binop" op [mode] operand "," operand
            | "unop" op [mode] operand
            | "cast" ckast [mode] operand "to" type
            | ("addr" | "addr.mut") place
            | "aggregate" type "(" [operand {"," operand}] ")"
            | "optional.some" operand | "optional.none" type
            | "result.ok" type [operand] | "result.err" type operand
            | "union.make" type int operand
            | "closure.make" ref [targs] "," operand
            | "dyn.make" ref "," operand
            | ("len" | "tag" | "haspayload") place | "errcode" operand
            | ("sizeof" | "alignof") type | "offsetof" type int
            | "call" callee "(" [operand {"," operand}] ")"
            | "intrinsic" ident "(" [operand {"," operand}] ")"
            | "comptime" ref
callee      = "direct" ref [targs] | "indirect" operand | "closure" operand
            | "dyn" operand int | "constrained" "param" ident type ident | "c.macro" string
mode        = "default" | "trap" | "wrap" | "assume"
```

- A field written `@n` has offset `n`: required for `layout explicit`, printed for every field in
  `Final`.
- `bytes` constants have type `slice<u8> const` (the length excludes the terminating NUL) or
  `ptr<u8> const nullable many`; the data always carries a terminating NUL.
- An aggregate constant lists the values of the fields, elements or `{has, value}` of its type in
  order; nested aggregates nest braces.
- The deref base takes a local only: a pointer held anywhere else is first copied into a temp.
- `dyn` callees name the method by its index in the interface; `union.make` names the member by
  its index in the union.
- A `source` line gives the full path, then the display path: the path relative to its package
  root with `/` separators, prefixed with `<PackageName>/` for packages other than the one being
  built. Panic messages and traces use the display path; `#line` uses the full path. Golden tests
  replace the repository root in full paths with `$ROOT` before comparing.
- Declarations are printed in this order: sources, cimports, then each table (structs, enums,
  unions, interfaces, opaques, globals, functions, impls, error names) sorted ordinally by key.

## In-memory representation

The IR is a portable project (D118), so the model is tables of structs and integer ids, never a
graph of objects. These rules are the representation; class names follow them.

- **Ids.** Each table has an id type, `readonly record struct StructId(int Index)` and so on for
  `EnumId`, `UnionId`, `InterfaceId`, `OpaqueId`, `GlobalId`, `FunctionId`, `ImplId`, `TypeId`,
  `SigId`, `TypeListId`, `ConstId`, `BytesId`, `StringId`, `SourceId`; inside a body `LocalId`,
  `BlockId`, `StatementId`, `PlaceId`, `OperandId`, `RvalueId`. `Index` is the position in the
  table; `None` is `-1`.
- **Module tables.** `Module` owns one `List<T>` per declaration kind, plus `Strings` (interned
  UTF-8 strings; keys and names are `StringId`), `Types`, `Sigs`, `TypeLists`, `Constants` and
  `Bytes`. A dictionary from key to id exists per declaration kind.
- **Interning.** `Types`, `Sigs`, `TypeLists` and `Constants` are interned: building a value that
  already exists returns the existing id, so equal ids mean structurally equal values and type
  equality is id equality. A type node is `{ TypeKind Kind; TypeFlags Flags; int A; int B; }`,
  where `A` and `B` are, by kind, a declaration id, an element `TypeId`, an array length, a `SigId`,
  or a `TypeListId` of type arguments. An error set type stores its member names as a sorted
  `TypeListId` of `StringId` values.
- **Declarations** are record structs whose list-valued members (fields, enum members, union
  members, interface methods, parameters, attributes) are ranges `{ int Start; int Count; }` into
  per-kind side tables of the module (`Fields`, `EnumMembers`, ...), appended when the declaration
  is built and never shared.
- **Bodies.** A `Body` owns `Locals`, `Blocks`, `Statements`, `Places`, `Projections`, `Operands`
  and `Rvalues`. A `Block` holds a `List<StatementId>` and a terminator; a pass that inserts
  statements appends them to `Statements` and rewrites the block's list. A `Place` is a base (local,
  global, or deref of a local) and a range of `Projections`. Operand and argument lists are ranges
  of `Operands`.
- **Spans** are `{ SourceId File; int Line; int Column; }`, 1-based; the column counts UTF-16 code
  units for C# sources (what Roslyn reports) and bytes for C headers (what clang reports).
- **Stages.** The core builds the `Instantiated` module as a new `Module` from the `Lowered` one;
  every later pass mutates the module in place and then sets `Stage`.
- **No behavior on the model.** Types and declarations carry data only. Algorithms live in the
  builder, the verifier, the printer, the parser and the passes.

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

- `ModuleBuilder`, `FunctionBuilder` (block cursor, local allocation, emission helpers), over the
  representation above.
- `IrPrinter`, `IrParser`.
- `IrVerifier.Verify(module, stage)`.
- `IBodyProvider`: implemented by frontends; `Body? Lower(FunctionId)` is how the core asks for the
  generic body of a function the first time an instantiation reaches it. The core substitutes type
  and comptime arguments itself, so a frontend lowers each body once.
- `Target`: triple, pointer size, endianness, C type sizes and alignments, and the C ABI rules for
  struct layout.
