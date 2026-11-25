# Decisions

Each decision has a number that never changes. A decision that is reversed stays here, marked
superseded, with the number of the one that replaces it.

## Language

**D001. A CΔ program is a valid C# program.** Roslyn reports no error on it. This gives every C#
editor, formatter, refactoring and analyzer to CΔ for free, and it is what "no new symbols" means
in practice.

**D002. Missing meanings come from intrinsics, attributes and reused C# names, in this order of
preference: an existing C# name with that meaning, a CΔ attribute, a CΔ intrinsic.** Examples:
`DllImport` for externs, `Unsafe.SkipInit` for undefined, `[Export]`, `Defer`.

**D003. The subset checker is an allowlist.** New C# versions add nothing until reviewed.

**D004. No user code can make syntax call user code invisibly.** Properties, indexers, operators,
conversions, events, finalizers and `Deconstruct` are removed. The corelib declares such members
because Roslyn needs them, and the compiler implements them as intrinsics. Exceptions kept:
`using` (the keyword announces the `Dispose` call) and interpolated string handlers (the `$`
announces the formatting calls).

**D005. `static class` is the module.** C# requires functions to live in a type; a static class
has no instances, so keeping it costs no object model.

**D006. Fields are always public.** No encapsulation of data.

**D007. Structs use C layout by default.** It is already the C# default for structs, and it makes
every struct C compatible.

**D008. `ref T` is the non-null single-item pointer, `T*` the C pointer, `Span<T>` the slice.** All
three exist in C# with these meanings.

**D009. `Span<T>` and `ReadOnlySpan<T>` are ordinary structs, not `ref struct`.** Slices must be
storable in structs. Escape checking for them is limited to obvious stack escapes (`CD2006`).

**D010. `Span<T>.Length` is `int`.** C# semantics, index and range support depend on it. The cost is
a 2^31 element limit per slice.

**D011. Text is UTF-8 bytes. `string` exists only as a constant.** A literal converts to
`ReadOnlySpan<byte>` and to `byte*` (NUL-terminated). This is the one place where CΔ defines what a
C# construct is in memory, because C#'s answer (a UTF-16 heap object) requires a runtime.

**D012. Integer arithmetic has three contexts, as C# has: default, `checked`, `unchecked`.** The
default context is illegal behavior on overflow (checked in safe modes), the choice C# already
leaves to a build option. `unchecked` wraps, `checked` always traps.

**D013. Build modes are Debug, ReleaseSafe, ReleaseFast, ReleaseSmall.**

**D014. No exceptions; errors are `Result` values.** Error sets are enums; error identity is the
member name across the program; codes are assigned globally by the compiler.

**D015. `Try` is an intrinsic call that returns from its caller.** C# has no expression that can,
and `try` is taken. Its name states the control flow.

**D016. `throw` survives only as `throw Panic(...)` and `throw Unreachable()`.** It gives the
noreturn flow analysis of C# for free.

**D017. A discarded `Result` is an error.**

**D018. Unused locals and unreachable code are errors.** The corresponding Roslyn warnings are
promoted.

**D019. Generics are always monomorphized and type-checked once, by C# rules.** Duck-typed
generics would break D001.

**D020. Interfaces have three uses: constraint (static dispatch), `Dyn<I>.Of(ref x)` (fat
pointer), and `[Union]` (tagged union by value).** The implicit struct to interface conversion
exists only for `[Union]`, where its C# meaning (a copy) is kept without the heap.

**D021. Delegates are values of two pointers (function and context), never multicast.**
Capturing lambdas capture variables by reference into a stack frame struct; escaping the frame is
the programmer's responsibility except for the direct escapes `CD2002` rejects.

**D022. Comptime is CΔ code interpreted over IR with target semantics.** Static initializers always
run at comptime; there is no static initialization at run time.

**D023. Comptime does not create types or declarations.** Roslyn must see every declaration
before comptime runs. Generics and build-time source generation cover the need.

**D024. `#if` is kept for build symbols only.** Roslyn type-checks every branch of a comptime `if`,
so code that only exists on one platform needs `#if` or a per-OS file.

**D025. Panics do not unwind and run no `Defer`.**

**D026. The language never allocates.** Every allocating std function takes an `IAllocator`;
allocation failure is a `Result`.

**D027. Imported C declarations keep their C names.** Idiomatic renaming is the job of projection
backends, not of the import.

**D028. `[Import]` is an attribute on a `static partial class`, not an expression.** An expression
cannot introduce declarations that Roslyn must see.

**D029. Imports are per target.** The C frontend reads headers for the build target, as the C
compiler will.

**D030. C function-like macros are not imported automatically; `[CMacro]` declares them.**

**D031. Exported signatures use only C types.**

**D032. `char` keeps its C# meaning, a UTF-16 code unit.** It is rarely useful in CΔ, and changing
it would break D001's promise that kept constructs mean what C# says.

**D033. User-defined operators are removed, including on generic math interfaces.** Generic
arithmetic works on primitives through corelib interfaces.

**D034. Fixed-size data uses `[InlineArray]`, `fixed` buffers, `stackalloc` and `static readonly`
arrays.** C# arrays are heap objects; only the static form has a static equivalent.

**D035. Overloading, optional parameters and named arguments are kept.** They are resolved at
compile time and hide no control flow.

## Compiler

**D101. Roslyn is the C# frontend.** Parsing, binding, checking and control flow lowering are
Roslyn's; the frontend lowers Roslyn's `ControlFlowGraph`.

**D102. Delta IR is a typed CFG over named locals, not SSA.**

**D103. Frontend, core and backends are separate projects, with dependencies enforced by
references.** Backends see only IR.

**D104. The first backend generates C11, and the C compiler does all optimization and code
generation.**

**D105. One translation unit per program.**

**D106. Every computed layout is asserted in the generated C.**

**D107. libclang reads C, through ClangSharp until the port of D119.** It also answers sizes, offsets and constant
values; nothing of C is reimplemented.

**D108. `zig cc` is the default C toolchain**, for cross compilation with C libraries of every
target out of the box. Clang, GCC and MSVC are alternatives.

**D109. The project file is an MSBuild SDK-style `.csproj` using `CDelta.Sdk`.** IDEs open it, NuGet
distributes packages, MSBuild orders builds.

**D110. Compilation is whole-program from sources.** Packages ship sources plus a reference
assembly for the IDE.

**D111. The same analyzer runs in the IDE and in `cdc`.**

**D112. `cdc` runs out of process from MSBuild.** MSBuild carries its own Roslyn.

**D113. The compiler is deterministic: identical inputs give identical C.**

**D114. The IR has one text format, printed and parsed, used for tests and dumps.**

**D115. kabic becomes the C frontend's tag reader plus projection backends, then is archived.**

**D116. The compiler is ported to CΔ, except a .NET host.** The host keeps what needs Roslyn or
MSBuild: `Frontend.CSharp`, `Frontend.CSharp.Rules` and `Build.Tasks`. Every other project is
written in C# until M7 and then ported to CΔ one component at a time. Writing the compiler in the
CΔ subset from the start is rejected: `Try`, `Defer` and spans as fields cannot run on .NET, and
the subset still changes before M7.

**D117. The IR is the boundary between the host and the native compiler.** The host lowers to IR
and passes it to the native core, a CΔ library with an `[Export]` C API that the host calls through
`[DllImport]`. The IR gains a binary format for that call. The text format of D114 stays the
format of tests and dumps.

**D118. Portable projects are written in a data-oriented style from the start**, so that porting
them is mechanical: structs and integer indices into arenas instead of object graphs, errors as
values, no exceptions for control flow, no LINQ, no reflection, no `async`.

**D119. The ported C frontend imports `clang-c/Index.h` with `[Import]`**, replacing ClangSharp.

**D120. The MVP is M0 to M3 plus a minimal C import, before generics.** Reading C is the reason to
pick CΔ, and command line tools over C libraries need no user generics: results, spans and
optionals are IR types.

**D121. The IR in memory is interned tables of structs addressed by integer ids, and every
declaration has a unique key.** It follows D118 and makes type equality an id comparison.
Integer constants are 128 bits, the widest IR integer.

**D122. Diagnostic messages are fixed per code; details go in notes.** The registry test can
then compare messages exactly, and `CDelta.Diagnostics` needs no Roslyn.

**D123. The compiler is single-threaded until M9.** Determinism is designed for parallelism,
which is an optimization.

**D124. Target data is a fixed table in the core, checked against clang by tests.** The core
knows no C compiler.

**D125. CI is `eng/ci.sh` in the distrobox `dev` until the repository has a remote.** Foreign tier
1 targets run under `qemu-aarch64` and `wine`.

**D126. A handled error removes its entries from the error return trace.** `Catch` and `TryGet`
save and restore the trace length, so a trace shows only the error in flight.

**D127. Implementation follows the documents and never decides design.** A gap or a conflict in
the documents is written to `docs/kanban/questions.md` and the affected work waits for the
answer.

## Repository

**D201. Documents and code are in English.**

**D202. The compiler targets .NET 10 and Roslyn pinned for C# 14.**

**D203. Status and plans in progress live in `docs/kanban/`, which is not versioned.**
