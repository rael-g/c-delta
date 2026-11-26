# Vision

## The problem

Write systems software (libraries, engines, tools, drivers of other native code) in a language
whose syntax and type system are already known by millions of programmers and already supported
by mature tooling, without paying for a garbage collector, a runtime, a virtual machine or an
object model, and without a wall between the new code and the C that already exists.

CΔ answers it in one sentence: **a subset of C#, compiled ahead of time to C, that reads C and is
read by C.**

## The north

Every question that the documents do not answer is decided by these three, in this order of
authority for their own domain:

1. **Philosophy: explicit, simple, allocation-aware systems programming.** No hidden control
   flow, no hidden allocation, no hidden runtime, errors as values, compile time code instead of a
   preprocessor, illegal behavior caught in safe builds. This is the philosophy of Zig and CΔ
   adopts it deliberately.
2. **Syntax and semantics: C#.** A CΔ program is a valid C# program. When a construct is kept, it
   means what C# says it means. When C# already has a construct for a meaning, CΔ uses it instead
   of inventing one. No divergence is silent: a kept construct behaves as in C#, or the difference
   stops the program from compiling; at run time it may differ only where C# would throw, and
   CΔ panics instead (D156).
3. **Interoperability: C.** C headers are a first-class input, the C ABI is the first-class output,
   and nothing in the language prevents calling or being called by C at zero cost.

When two of them conflict, CΔ removes the C# feature rather than bending its meaning. A C#
construct is either kept with its C# meaning, kept with a restriction that only rejects programs,
or removed. The few places where a construct must mean something C# never defined (for example,
what a `string` literal is in memory) are listed in [decisions.md](decisions.md).

Everything else is an implementation detail.

## Principles

These follow from the north and decide most design questions directly.

- **Remove, never add.** CΔ adds no symbol, keyword or grammar to C#. What C# cannot express is
  expressed by an intrinsic function, an intrinsic type or an attribute, all ordinary C# names.
- **Reuse before inventing.** If C# or its standard API already has a name for a meaning
  (`DllImport`, `StructLayout`, `Unsafe.SkipInit`, `Interlocked`), CΔ uses that name with that
  meaning.
- **Valid C# at all times.** Roslyn must report no error on a CΔ program. This keeps every C#
  editor, formatter and refactoring working on CΔ code for free.
- **Reading code beats writing it.** A call looks like a call. An operator calls user code only on
  a user type, where it reads as that type's operation, and only through its operands. A field
  access never runs code. Anything that allocates takes an allocator.
- **Edge cases are the design.** Overflow, out of range, null, an unmatched switch and division by
  zero have a defined answer in every build mode.
- **A compile error beats a runtime crash, and a runtime crash beats a silent bug.**
- **Memory is a resource and allocation may fail.** Every allocation returns a `Result`.
- **One obvious way.** When C# offers two spellings for one meaning, CΔ keeps both only if both
  are pure syntax; when one of them carries hidden behavior, it goes.
- **Lazy by design.** The compiler delegates every solved problem: Roslyn parses, binds and
  checks C#; libclang reads C; a C compiler optimizes, generates machine code and handles ABIs;
  MSBuild and NuGet build and distribute; C libraries are wrapped instead of rewritten.
- **Separation.** Frontends know their source language and nothing about targets. The core knows
  only Delta IR. Backends know their target and nothing about source languages.

## What CΔ is

- A compiled language with no mandatory runtime. A freestanding CΔ program depends on no library
  at all, not even libc.
- A procedural language with value types, explicit memory, generics, interfaces as contracts,
  delegates and lambdas, attributes, error unions and comptime.
- A C peer: imports C headers, compiles C sources, emits C, exports C ABI functions and headers.
- A compiler platform: the IR is meant to carry other source languages in (C, and later others) and
  other languages out (C, and later idiomatic bindings in C#, Zig and others), which is why kabic
  will become part of it.

## What CΔ is not

- Not object oriented: no classes, no inheritance, no virtual dispatch through a class hierarchy,
  no encapsulation of fields.
- Not .NET: it does not run on the CLR or Mono, does not use the BCL, does not produce assemblies
  to execute, and does not interoperate with managed code.
- Not a dialect: it has no syntax of its own.
- Not fully self-hosted: a small .NET host keeps Roslyn and MSBuild; the rest of the compiler is
  ported to CΔ after M7 (D116).
- Not a C replacement for every C feature: inline assembly, `long double`, and arbitrary
  preprocessor macros are reached through C sources, not through CΔ syntax.

## Names

- The language is **CΔ**, written **C Delta** or **cdelta** where Δ cannot be used.
- The repository and directory are `c-delta`.
- The compiler command is `cdc`.
- The .NET namespace of the compiler and of the core library is `CDelta`.
- The IR is **Delta IR**.
- Source files use the `.cs` extension, so C# tooling recognizes them.

## Glossary

- **Corelib**: the reference assembly that declares the types Roslyn requires (`System.Object`,
  `System.Int32`, `Span<T>`) and the CΔ intrinsics. It contains declarations only; the compiler
  implements every member.
- **Std**: the standard library, written in CΔ.
- **Intrinsic**: a corelib member whose meaning is implemented by the compiler.
- **Illegal behavior**: an operation whose result the language does not define (overflow in the
  default context, out-of-range index). Safe build modes detect it and panic; fast modes assume it
  does not happen.
- **Panic**: an unrecoverable stop of the program. It does not unwind and runs no `Defer`.
- **Comptime**: evaluation performed by the compiler while compiling.
- **Monomorphization**: compiling one copy of a generic for each set of type and comptime
  arguments it is used with.
- **Package**: a unit of compilation and distribution, one project file.
