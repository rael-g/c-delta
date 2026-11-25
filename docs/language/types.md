# Types

## Primitives

Every primitive keeps its C# size and meaning. The C column is what the C backend emits.

| CΔ | Size | Meaning | C |
|---|---|---|---|
| `bool` | 1 | `false` or `true` | `bool` |
| `sbyte`, `short`, `int`, `long` | 1, 2, 4, 8 | signed two's complement | `int8_t` ... `int64_t` |
| `byte`, `ushort`, `uint`, `ulong` | 1, 2, 4, 8 | unsigned | `uint8_t` ... `uint64_t` |
| `nint`, `nuint` | pointer | signed and unsigned pointer-sized | `intptr_t`, `uintptr_t` |
| `Int128`, `UInt128` | 16 | 128-bit integers | `__int128` or a struct where unsupported |
| `Half`, `float`, `double` | 2, 4, 8 | IEEE 754 binary16, binary32, binary64 | `_Float16` or a struct, `float`, `double` |
| `char` | 2 | a UTF-16 code unit, as in C# | `uint16_t` |
| `CLong`, `CULong` | target | C `long` and `unsigned long` | `long`, `unsigned long` |
| `void` | 0 | no value | `void` |

`decimal` does not exist. `Void` (corelib struct, size 0) is the value form of `void`, used as a
type argument: `Result<Void, E>`.

## Structs

A struct is a value. Assigning or passing it copies it. Fields are always `public`.

```csharp
struct Vec2
{
    public float X;
    public float Y;

    public Vec2(float x, float y) { X = x; Y = y; }

    public readonly float Dot(Vec2 other) => X * other.X + Y * other.Y;
    public void Scale(float k) { X *= k; Y *= k; }
}
```

- Instance methods receive `this` by reference (`ref` for mutable methods, `ref readonly` for
  `readonly` methods), as in C#.
- `new Vec2(1, 2)`, `new Vec2 { X = 1 }`, `default` and `new()` construct on the stack or in
  place. Nothing allocates.
- `static` fields of a struct are globals scoped to it. Generic structs have one copy per
  instantiation.

### Layout

| Declaration | Layout |
|---|---|
| no attribute, or `[StructLayout(LayoutKind.Sequential)]` | C layout: declaration order, natural alignment. This is the C# default for structs and it makes every struct C compatible. |
| `[StructLayout(LayoutKind.Sequential, Pack = n)]` | C layout with maximum alignment `n` |
| `[StructLayout(LayoutKind.Auto)]` | The compiler may reorder fields to minimize padding |
| `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset]` | Fields at the given offsets; all at `0` is an untagged union |
| `[Bits(n)]` on integer fields of a sequential struct | C bit-field of width `n`, laid out as the target C compiler does |
| `[StructLayout(..., Size = n)]` | Minimum size `n` |

A `[FieldOffset]` need not be aligned: such a field is read and written in place, as packed C
does. Taking a `ref` or a pointer to it is `CD2008`, because the address would be misaligned.

Sizes and offsets are computed by the compiler for the target and checked against the C compiler
with static assertions in the generated C ([../compiler/backend-c.md](../compiler/backend-c.md)).

### Opaque structs

`[Opaque] struct Handle { }` declares a type whose size is unknown, like an incomplete C struct.
It may only be used behind a pointer. `CD2003`

## References, pointers and slices

| CΔ | Meaning | Nullable | C |
|---|---|---|---|
| `ref T`, `out T` | a pointer to exactly one `T` | no | `T*` |
| `ref readonly T`, `in T` | a pointer to one `T` that is not written through | no | `const T*` |
| `T*` | a C pointer: to one or many `T`, arithmetic allowed | yes | `T*` |
| `void*` | an untyped C pointer | yes | `void*` |
| `Span<T>` | pointer and length, elements writable | empty | `struct { T* ptr; int32_t len; }` |
| `ReadOnlySpan<T>` | pointer and length, elements read only | empty | `struct { const T* ptr; int32_t len; }` |
| `delegate*<...>` | a function pointer | yes | `R (*)(...)` |

- `ref` follows every C# rule, including ref safety analysis of `ref struct` and `scoped`.
- Pointer types require an `unsafe` context, as in C#. The idiom is to mark the containing
  `static class` or struct `unsafe`.
- `T*` requires `T : unmanaged` for a type parameter, as in C#. Every CΔ type is unmanaged except
  those Roslyn considers managed: structs that contain a delegate or an interface value. Those are
  reached with `ref`, never `T*`.
- `Span<T>` and `ReadOnlySpan<T>` are ordinary structs in CΔ, not `ref struct`: they may be fields
  of any struct. The compiler checks only the obvious escape of stack memory (`CD2006`): returning
  `stackalloc` or the address of a local.
- Because spans are not ref structs, Roslyn does not convert `stackalloc` to a span (`CS8346`);
  `stackalloc` has only its pointer form, and a span over it is built with
  `new Span<T>(pointer, length)`.
- `Length` is `int`, as in C#. Indexing, slicing (`s[a..b]`, `s[^1]`), `foreach` and list patterns
  are intrinsics; indexing is bounds-checked in safe modes.
- `fixed (T* p = span)` takes the address of the first element. Nothing is pinned because nothing
  moves.
- Dereferencing a null `T*` is illegal behavior.

## Arrays

C# arrays are heap objects, so the general `T[]` is removed. Fixed-size data uses:

| Need | CΔ |
|---|---|
| Inline array of any element type | `[InlineArray(16)] struct Buf16 { int _e; }` |
| Inline array of a primitive inside an `unsafe` struct | `fixed byte Name[32];` |
| Stack array in a function | `Span<int> s = [1, 2, 3];`, or in an `unsafe` context `int* p = stackalloc int[n];` then `var s = new Span<int>(p, n);` |
| Global table | `static readonly int[] Primes = [2, 3, 5, 7];` |

A `static readonly` array is static data of constant length, mutable element by element, and
converts implicitly to `Span<T>` and `ReadOnlySpan<T>`. `new T[n]` does not exist; dynamic arrays
come from an allocator ([memory.md](memory.md)).

## Enums

C# enums with their underlying integer type.

- An enum declared in CΔ without `[Flags]` or `[NonExhaustive]` is **exhaustive**: a conversion
  from an integer to it is checked in safe modes, and a `switch` over it must name every member or
  have `default` (`CD2001`).
- `[Flags]`, `[NonExhaustive]` and every enum imported from C are **non-exhaustive**: any value of
  the underlying type is valid, and a `switch` over them needs `default`.

## Optionals

`T?` for a value type `T` is `Nullable<T>`: a `T` and a `bool`. `null`, `??`, `??=`, `?.`,
`HasValue`, `Value`, `GetValueOrDefault()` and patterns (`x is { } v`, `x is null`) keep their C#
meaning. `Value` on `null` is illegal behavior.

Pointers are already nullable; `T*?` does not exist, as in C#.

## Tuples

`(int Count, float Mean)` is an anonymous struct with C layout. Tuple deconstruction
(`var (a, b) = t;`) is kept.

## Text

There is no string type at run time. Text is UTF-8 in `ReadOnlySpan<byte>` (read only) or
`Span<byte>`.

A `string` literal or `const string` is a compile-time value with these conversions, declared by the
corelib:

| Target | Result |
|---|---|
| `ReadOnlySpan<byte>` | the UTF-8 bytes in static memory, without the terminating NUL in `Length` |
| `byte*` | a pointer to the same bytes, NUL-terminated, for C |

The compiler re-encodes the UTF-16 literal of C# into UTF-8 at compile time; a literal containing
a lone surrogate is an error (`CD2007`). `"text"u8` is also kept and means the same `ReadOnlySpan<byte>`.

A `string` cannot be the type of a local, field, parameter or return (`CD1020`), and only constants
concatenate (`CD1021`). Comparing text is `Std.Mem.Eql(a, b)`.

An interpolated string is allowed only when converted to an interpolated string handler type,
which is how the std formats without allocation ([std.md](../std.md)).

## Integer semantics

Integer promotion, implicit widening and the result types of operators are exactly those of C#
(`byte + byte` is `int`, `int` converts implicitly to `long` and `float`).

Every arithmetic operation and numeric cast happens in one of three contexts:

| Context | How it is written | Overflow or truncation |
|---|---|---|
| default | no `checked` or `unchecked` around it | illegal behavior: panic in Debug and ReleaseSafe, assumed impossible in ReleaseFast and ReleaseSmall |
| `checked` | inside `checked(...)` or a `checked { }` block | always a panic, in every mode |
| `unchecked` | inside `unchecked(...)` or an `unchecked { }` block | wraps (two's complement) or truncates |

This is the C# model with its default context made a build mode choice, which C# already allows
with its overflow-checking option. Concretely:

- `+ - *` and unary `-`, integer conversions between types, `++`, `--`, and compound assignments
  follow the table.
- Division or remainder by zero is illegal behavior in every context; `int.MinValue / -1` too.
- Shifts mask the count, as in C#: `x << 33` on an `int` shifts by `1`. Never illegal.
- A float to integer conversion out of range or `NaN`: illegal in the default context, a panic in
  `checked`, saturating (with `NaN` to `0`) in `unchecked`.
- An integer to exhaustive enum conversion with no member of that value: illegal in the default
  context, a panic in `checked`, allowed in `unchecked`.

## Floating point semantics

IEEE 754 with round to nearest and no contraction (no fused multiply-add unless written with
`Math.FusedMultiplyAdd`), as in C#. Division by zero gives infinities or `NaN`, never illegal.

## Conversions

Implicit and explicit conversions are C#'s, minus those that cannot exist (boxing, reference
conversions). Additional CΔ rules:

- A struct converts implicitly to a `[Union]` interface it implements (copying it in). No other
  struct to interface conversion exists (`CD5001`); see
  [generics-interfaces-delegates.md](generics-interfaces-delegates.md).
- `Unsafe.BitCast<TFrom, TTo>` reinterprets bits between types of equal size.
- `Unsafe.As<TFrom, TTo>(ref x)` reinterprets a reference.
- Pointer to integer and integer to pointer casts are kept, as in C#.

## Evaluation order

Operands, arguments and side effects are evaluated strictly left to right, as in C#. The C backend
guarantees it by sequencing through temporaries; nothing depends on C's unspecified order.
