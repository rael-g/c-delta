# Delta IR

Delta IR is the only thing the core sees and the only thing a backend or a projection receives. It must carry a
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

### Growth

C is the floor of the IR, not its ceiling: the IR already keeps generics, results, slices,
optionals, unions, interface values and closures as what they are, so the comptime interpreter
runs them, projections see them and the core checks them (D171). It grows by three rules:

1. A construct enters when a real frontend needs it, never in anticipation.
2. Every construct has a lowering to C11 and a meaning in the comptime interpreter, because the C
   backend is the universal backend.
3. A construct that serves one language lives in that language's dialect: attributes and
   intrinsics in its namespace, lowered by its frontend into the common IR before the core runs.
   It moves into the common IR only when more than one frontend needs it or backends and
   projections need its meaning.

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
| `slice<T>` with flag `const` | pointer and `i32` length; `slice<u8> const text` is a `string`, the same value marked as text for projections and diagnostics |
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

Parameter passing modes are part of the signature: `value`, `ref`, `ref.readonly`, `out`. A
parameter with a mode other than `value` has a pointer type in the IR: `ref T` and `out T` are
`ptr<T>`, `ref readonly T` and `in T` are `ptr<T> const`, never nullable or many. The body reads
and writes through it with a deref place (`(*%0)`), and a caller passes `addr` of the argument's
place. The mode is kept beside the type for diagnostics, for `out` definite assignment, and for
projections; the C backend ignores it.

- `this` of an instance method is the first parameter, named `this`, with mode `ref` and type
  `ptr<S>`, or mode `ref.readonly` and type `ptr<S> const` for a `readonly` method or a member of a
  `readonly struct`.
- A `ref` local is a local of type `ptr<T>` (`const` for `ref readonly`) assigned with `addr`.
- A `ref` return has a return mode in the signature, `ref` or `ref.readonly`, and returns
  `ptr<T>`.

Pointer operations:

| Source | IR |
|---|---|
| `p + n`, `p - n`, `n + p` on `T*` | `binop add` or `sub` with the pointer first and an `isize` operand (the frontend converts `n` with `cast int.int wrap`), scaled by the element size as in C |
| `p - q` | `binop sub` of two pointers of the same type, giving `isize` elements |
| `p == q`, `!=`, `<`, `<=`, `>`, `>=` | `binop eq ne lt le gt ge` on two pointers of the same type, or a pointer and `const null` |
| `p[i]` | the pointer `p + i` into a temp, then the place `(*%t)` |
| `p->f` | the place `(*%p).field(n)` |
| `sizeof(T)`, `Unsafe.SizeOf<T>()` | `sizeof T`, of type `i32` |

Pointer operations have no mode and no safety check: an offset outside the object is illegal
behavior that is not detected.

Slices, stack memory and optionals:

| Source | IR |
|---|---|
| `new Span<T>(p, n)`, `new ReadOnlySpan<T>(p, n)` | `aggregate slice<T>(p, n)` (`slice<T> const` for a read-only span): the pointer as `ptr<T> nullable many` and the `i32` length |
| `default(Span<T>)`, `[]` to a span | `const slice<T> {null, 0}` |
| `s[a..b]`, `s[a..]`, `s[..b]`, `s[^k..]`, `s.Slice(a, n)`, `s.Slice(a)` on a slice or array place | `slice(place, start, end)`, `start` and `end` as `i32`: `^k` is `len place` minus `k`, an omitted start is `0`, an omitted end is `len place`, `Slice(a, n)` ends at `a + n` (`binop add default`) |
| `s[^k]` | the place `place[len place - k]` |
| a static array or inline array converted to `Span<T>` or `ReadOnlySpan<T>` | `slice(place, 0, N)` over the array's place, so the slice points at its storage |
| a collection expression converted to `Span<T>` or `ReadOnlySpan<T>` | a `temp` local of type `array<T, N>` assigned an `aggregate` of the elements (a constant when every element is constant), then `slice(%arr, 0, N)` |
| `fixed (T* p = s)` over a span | `cast slice.ptr` |
| a `string` converted to `ReadOnlySpan<byte>`, or back | `cast bitcast` between `slice<u8> const text` and `slice<u8> const` |
| `a == b`, `a != b` on strings, and a string constant pattern | `intrinsic text.eql(a, b)` of type `bool`, negated by `unop not` for `!=` |
| `s.Bytes` | `cast bitcast` to `slice<u8> const` |
| `s.Slice(start, count)` | `slice(s, start, start + count)` with `binop add default` |
| a switch expression whose arms match nothing | `panic` with "no switch arm matched", where Roslyn's graph throws |
| `foreach (var r in s)` over a string | an `i32` offset local from `0`; while `offset < len s`, `intrinsic utf8.next(s, addr.mut %offset)` gives the scalar as `i32` and advances the offset; the `Rune` is `aggregate struct System.Text.Rune(scalar)` |
| `fixed (T* p = a)` over a static array | `addr.mut a[0]` then `cast ptr.ptr` |
| `stackalloc T[N]` with a constant `N` outside every loop, with or without an initializer | a `temp` local of type `array<T, N>` assigned the zero constant of its type, the initializer's `aggregate`, or nothing under `[SkipLocalsInit]`; then `addr.mut %arr[0]` and `cast ptr.ptr` to `T*` |
| `stackalloc T[n]` with a run-time `n`, or any `stackalloc` inside a loop | `alloca T, n, init`: `n` converted to `usize` with `cast int.int default`, `init` `zero`, or `undef` under `[SkipLocalsInit]` |
| `x.HasValue`, `x != null`, `x is not null` | `haspayload x`, negated by `unop not` for `== null` and `is null` |
| `x.Value`, and a cast from `T?` to `T` | the place `x.payload(0)` |
| `x.GetValueOrDefault()`, `x.GetValueOrDefault(d)` | `branch` on `haspayload x`: one block copies `x.payload(0)`, the other the zero constant of `T` or `d` |
| `new T?(v)`, a conversion from `T` to `T?` | `optional.some v` |
| `null` or `default` of `T?` | `optional.none T` |

- The payload of an optional is variant `0`. `?.` and `??` reach the IR already expanded by
  Roslyn into these forms; lifted operators are expanded by the frontend
  ([frontend-csharp.md](frontend-csharp.md#lowering-bodies)).
- `slice` takes a slice or array place and gives a slice of the same element type (`const` when
  the place's slice is `const` or the destination is a read-only span) over the elements
  `start` to `end` excluded.
- `alloca` gives `ptr<T> nullable many` to `count` elements of fresh stack memory that lives
  until the function returns. `init` is `zero`, `undef` or `fill`; only the core writes `fill`
  ([core-passes.md](core-passes.md#8-debug-instrumentation)).
- The array local of a collection expression lives for the whole body, with no `live` and
  `dead`: one storage per source expression, reused by every evaluation of it, as in C#. A
  constant `stackalloc` outside every loop is such a local too; inside a loop it is `alloca`, so
  each evaluation gets new memory as in C#.

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
          | len(Place) | tag(Place) | haspayload(Place) | err(Place) | errcode(Operand)
          | slice(Place, Operand start, Operand end)
          | alloca(Type, Operand count, init)
          | sizeof(Type) | alignof(Type) | offsetof(Type, field)
          | call(Callee, Operand...)
          | intrinsic(name, Operand...)
          | comptime(Function)                 Lowered only
```

- `op` on integers: `add sub mul div rem shl shr and or xor eq ne lt le gt ge neg not bitnot`;
  signedness comes from the operand type. On floats: `fadd fsub fmul fdiv frem fneg feq fne flt
  fle fgt fge`. `not` is on `bool` only.
- `mode` for integer `add sub mul neg` and the casts `int.int` and `float.int`:
  `default` (Lowered and Instantiated only), `trap` (always checked; what a C# `checked` context
  gives), `wrap` (what `unchecked` gives; saturating for `float.int`), `assume` (illegal behavior
  assumed impossible, never written by the core for arithmetic). The core rewrites `default` to `trap` or `wrap` by build mode and safety
  overrides. Other operations have no mode.
- `cast kind`: `int.int`, `int.float`, `float.int`, `float.float`, `int.enum`, `enum.int`,
  `ptr.int`, `int.ptr`, `ptr.ptr`, `bitcast`, `slice.ptr`, `enum.error`, `error.enum`,
  `error.error`. `slice.ptr` gives the pointer a slice
  holds, as `ptr<T> nullable many` (`const` for a `const` slice).
- `Callee`: `direct(Function<args>)`, `indirect(Operand)` for function pointers,
  `closure(Operand)`, `dyn(Operand, method)`, `constrained(param T, Interface.method)` (Lowered
  only, resolved by instantiation), `c.macro(text, types)`: the macro's name, or with `template` its
  template, and the C types of the instance's type arguments.
- Constants: integers (128 bits of two's complement with a type, the widest IR integer), floats (bit pattern), `bool`, `null`,
  bytes (static data, with or without NUL), aggregates, pointers to globals with an offset,
  function references, error codes, `undef`.

Errors and results:

- A value of type `errorset{names}` or `anyerror` is one error. The frontend writes
  `errorset(E)` for an enum `E` used as a set: `errorset` of its member names, sorted ordinally.
  `error.Name` constants are symbolic (`const errorset{A, B} error A`) in every stage; the
  numbers come from the `errorname` items the core adds in `Final`
  ([core-passes.md](core-passes.md#4-error-codes)). An error value is never the success code `0`.
- `cast enum.error` turns a value of an enum `E` into the error of the same member name, typed
  `errorset(E)`; `cast error.enum` turns an `errorset(E)` value back into the member of `E` with
  that name. Neither looks at the enum's declared values. A value of a non-exhaustive `E` that
  names no member gets a `check enum` before `enum.error`.
- `cast error.error` changes the set of an error value to a set that contains all its names, or
  to `anyerror`; the core rejects any other target. It changes no bits.
- `errcode` gives the `u16` code of an error value; it is valid only in `Final`.

| Source | IR |
|---|---|
| a `T` converted to `Result<T>` or `Result<T, E>` | `result.ok type v` |
| `default` of a result type, `return default;` | `result.ok type` with the zero constant of `T`; `result.ok type` with no operand when `T` is `void` |
| an `E` converted to `Result<T, E>` | `cast enum.error`, then `result.err type e`; a constant member folds to an `error` constant |
| `Fail(e)` | `cast enum.error`, then `cast error.error` to `anyerror`: the corelib `Error` is `anyerror` |
| `Error` converted to `Result` or `Result<T>` | `result.err type e` |
| `r.IsError` | `haspayload r`, then `unop not` |
| `r.Value` | the place `r.payload(0)` |
| `r.Error` on `Result<T, E>` | `err r`, then `cast error.enum` to `E` |
| `r.Error` on `Result` or `Result<T>` | `err r`, which is already `anyerror` |
| `r.TryGet(out v, out e)` | `trace.save`; `branch` on `haspayload r`; success: `v` gets `r.payload(0)`, the result is `true`; error: `v` gets the zero constant of `T`, `e` gets `r.Error` as above, `trace.restore`, the result is `false` |
| `Catch(r, d)` | `trace.save`; `branch` on `haspayload r`; success: `r.payload(0)`; error: `trace.restore`, then `d` |
| `Try(r)` in a function returning `result<U, F>` | `branch` on `haspayload r`; success: `r.payload(0)`; error: `err r`, `cast error.error` to `F` (or `anyerror`), the deferred bodies, `trace.push`, `return result.err` |
| `error.Name` | `intrinsic error.name(e)`, of type `slice<u8> const`; the core replaces it by an index into the name table |
| an error from `Main`, in the synthetic entry point only | the statement `intrinsic error.report(name)`, with `name` a `slice<u8> const`: writes `error: <name>` and the trace block to standard error ([backend-c.md](backend-c.md#output-of-an-error-from-main)) |

- On a `result` place, `haspayload` is true on success and `payload(0)` is the `T`; `err` gives
  the error, typed with the result's error set. A `result` of one type never converts to another
  except through these reads, as `Try` does.

### Statements and terminators

```
Statement   := Place = Rvalue
             | call(Callee, Operand...)                 result discarded
             | intrinsic(name, Operand...)               an intrinsic of type void
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
Attribute  name (namespaced: cs.Obsolete, cdelta.Export, ann.out, proj.cs.name), arguments
           (positional and named constants)
```

The IR never interprets an attribute it does not own; the namespace says who does. This is how
annotations travel from any frontend to the projections without new IR constructs
([annotations](#annotations)).

## Text format

```
module Hello stage final target x86_64-linux-gnu

source 1 "$ROOT/samples/hello/src/Program.cs" "src/Program.cs"

struct Hello.Vec2 layout c size 8 align 4 @1:3:8 {
  X: f32 @0
  Y: f32 @4
}

fn "Hello.Vec2.Dot(Hello.Vec2)"(this: ref.readonly ptr<struct Hello.Vec2> const, other: struct Hello.Vec2) -> f32 @1:7:27 {
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
field       = attrs ident ":" type ["bits" int ["at" int]] ["@" int] NL
enum        = attrs "enum" ref ":" type ["exhaustive"] [origin] span "{" NL {ident "=" int NL} "}" NL
union       = attrs "union" ref ["tag" type "size" int "align" int] span "{" NL {type ["=" int] NL} "}" NL
interface   = attrs "interface" ref [tparams] ["extends" type {"," type}] span "{" NL {imethod} "}" NL
imethod     = ["static"] "fn" ident sig ["default" ref] NL
opaque      = attrs "opaque" ref [origin] span NL
global      = attrs "global" ref ":" type {gflag} ["=" (constant | "comptime" ref)] [origin] span NL
gflag       = "mutable" | "threadlocal" | linkage | "section" string | "align" int
function    = attrs "fn" ref [tparams] "(" [param {"," param}] ["," "..."] ")" "->" [pmode] type
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
            | "slice" "<" type ">" ["const" ["text"]]
            | "array" "<" type "," int ">"
            | "fn" sig | "closure" sig
            | "dyn" "<" type ">"
            | "optional" "<" type ">"
            | "result" "<" type "," type ">"
            | "errorset" "{" ident {"," ident} "}" | "anyerror"
            | ("struct" | "interface") ref [targs] | ("enum" | "union" | "opaque") ref
            | "param" ident | "comptime.type" | "comptime.typeinfo"
int-type    = "i8" | "i16" | "i32" | "i64" | "i128" | "u8" | "u16" | "u32" | "u64" | "u128"
sig         = "(" [[pmode] type {"," [pmode] type}] ["," "..."] ")" "->" [pmode] type ["cc" conv]

body        = "{" NL {local} block {block} "}" NL
local       = "local" lref [ident] ":" type ("param" | "user" | "temp") [span] NL
block       = bref ":" NL {stmt} term
stmt        = ( place "=" rvalue
              | "call" callee "(" [operand {"," operand}] ")"
              | "intrinsic" ident "(" [operand {"," operand}] ")"
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
            | "cast" castkind [mode] operand "to" type
            | ("addr" | "addr.mut") place
            | "aggregate" type "(" [operand {"," operand}] ")"
            | "optional.some" operand | "optional.none" type
            | "result.ok" type [operand] | "result.err" type operand
            | "union.make" type int operand
            | "closure.make" ref [targs] "," operand
            | "dyn.make" ref "," operand
            | ("len" | "tag" | "haspayload" | "err") place | "errcode" operand
            | "slice" place "," operand "," operand
            | "alloca" type "," operand "," ("zero" | "undef" | "fill")
            | ("sizeof" | "alignof") type | "offsetof" type int
            | "call" callee "(" [operand {"," operand}] ")"
            | "intrinsic" ident "(" [operand {"," operand}] ")"
            | "comptime" ref
callee      = "direct" ref [targs] | "indirect" operand | "closure" operand
            | "dyn" operand int | "constrained" "param" ident type ident | "c.macro" ["template"] string [targs]
mode        = "default" | "trap" | "wrap" | "assume"
castkind    = "int.int" | "int.float" | "float.int" | "float.float" | "int.enum" | "enum.int"
            | "ptr.int" | "int.ptr" | "ptr.ptr" | "bitcast" | "slice.ptr" | "enum.error"
            | "error.enum" | "error.error"
```

- A field written `@n` has byte offset `n`: required for `layout explicit`, printed for every field
  that is not a bit-field in `Final`.
- A bit-field is written `bits w at b`: width `w` and offset `b` in bits from the start of the
  struct. `at` is printed for every bit-field in `Final` and never with `@`. In memory a field has
  a byte offset and, for a bit-field, a width and a bit offset.
- `bytes` constants have type `slice<u8> const` (the length excludes the terminating NUL) or
  `ptr<u8> const nullable many`; the data always carries a terminating NUL.
- An aggregate constant lists the values of the fields, elements, `{ptr, len}` or `{value, has}` of its type in
  order; nested aggregates nest braces.
- The deref base takes a local only: a pointer held anywhere else is first copied into a temp.
- `dyn` callees name the method by its index in the interface; `union.make` names the member by
  its index in the union.
- A `source` line gives the full path, then the display path: the path relative to its package
  root with `/` separators (with `..` segments for a file outside the root), prefixed with `<PackageName>/` for packages other than the one being
  built. Panic messages and traces use the display path; `#line` uses the full path. Golden tests
  replace the repository root in full paths with `$ROOT` before comparing.
- Declarations are printed in this order: sources, cimports, then each table (structs, enums,
  unions, interfaces, opaques, globals, functions, impls, error names) sorted ordinally by key.

## Annotations

An annotation is an IR attribute in the `ann.` namespace that states a fact about a declaration
which its source language does not express: a pointer written back, a pointer and a count forming
one sequence, an owner, a borrowed or optional value, an error convention (D170). The vocabulary is
defined on IR concepts, never on the syntax of a language, so annotations of any frontend reach
every projection and plugin. It is designed in M8 by the clean room of [../kabic-migration.md](../kabic-migration.md#the-clean-room).

Annotations come from three sources:

- comments of the source language, such as the doc tags of a C header;
- attributes in CΔ, where a CΔ type does not already say it, which is rare, since spans,
  `Result` and optionals carry most of these facts;
- an annotation manifest, a file beside the project that names declarations by their IR key
  (`c:sqlite3_open`, `MyLib.Parser.Parse(string)`) and so serves a header that cannot be edited and
  any language alike. A key that names no declaration is an error, so a manifest never goes
  stale silently.

## Stability

The IR has two layers with different promises (D168):

| Layer | What | Promise |
|---|---|---|
| declarations | types, globals, function signatures, impls, attributes, annotations, error names | what projections and plugins consume; from 1.0 it changes only by addition, and every external format of it carries a format version that `cdc` keeps reading |
| bodies | locals, blocks, statements, terminators, the stages | internal; no promise until a frontend outside the compiler exists, which is then a decision |

The external formats are the plugin protocol of [../kabic-migration.md](../kabic-migration.md#the-command)
from M8 and the binary format of M10. The text format of this document gains a `format <n>`
line in its header when it becomes one of them.

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
