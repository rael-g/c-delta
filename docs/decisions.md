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

**D004. No user code can make syntax call user code invisibly.** Properties, indexer setters, implicit
conversions, events, finalizers and `Deconstruct` are removed. The corelib declares such members
because Roslyn needs them, and the compiler implements them as intrinsics. Exceptions kept:
`using` (the keyword announces the `Dispose` call), interpolated string handlers (the `$`
announces the formatting calls), operators and explicit conversions on user types (D143),
indexer getters (D150), and getter-only static abstract properties with their implementations
(D153).

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
run at comptime; there is no static initialization at run time. The std relies on it for what .NET leaves to run time: a format specifier, literal in every
interpolation, is checked against its type when compiling, and Unicode tables are computed from
the Unicode data files read with `Embed`, so no generated file is kept and no table is built at
startup.

**D023. Comptime does not create types or declarations; Roslyn source generators do.** Roslyn
must see every declaration before comptime runs. A tool outside the compiler sees no semantic
model and no IDE; an incremental generator sees both and already runs in the IDE, because a CΔ
project is a C# project. `cdc` runs a project's generators as the IDE does, and their output is
CΔ checked like any other source. A generator writes declarations and comptime computes what can
be computed from `TypeInfo`. The repository and the SDK use them where a name must exist:
`CDelta.Diagnostics` from the registry of `diagnostics.md`, so the normative table is the only
place a code is added; typed fields for build options, so their names are checked by C#; and
later, a member per asset file.

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

**D030. C function-like macros are not imported automatically; `[CMacro]` declares them, in
every form.** A macro has no type, so its signature is written by hand, but no shape is out of
reach: a `ref` parameter is the lvalue the macro writes, a `void` one is a statement, a generic
one is instantiated like any generic with each expansion checked by the C compiler, and a
template spells the macros that take types or are not shaped like a call. Macros that declare
need nothing, since the header is read after preprocessing.

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

**D103. Frontends, core, backends and projections are separate projects, with dependencies
enforced by references.** Backends and projections see only IR. A backend receives the final IR,
bodies included, and compiles the whole program to a target; a projection reads only the
declaration layer (D168) and writes declarations in another language so that code there reaches
a surface: the import stubs, the CΔ and C# projections, Zig and plugins. Their inputs, guarantees
and users differ, so they are named apart: `CDelta.Projection.CSharp` and `CDelta.Projection.Zig`,
over the shared `CDelta.Projection`.

**D104. The first backend generates C11, and the C compiler does all optimization and code
generation.**

**D105. One translation unit per program, plus a second with what needs libc headers.** The
program's unit includes no header that declares functions, so `[DllImport]` prototypes never
conflict with libc's (D129).

**D106. Every computed layout is asserted in the generated C.**

**D107. libclang reads C, through ClangSharp until the port of D119.** It also answers sizes, offsets and constant
values; nothing of C is reimplemented. ClangSharp is only the binding of `Frontend.C`: the raw
layer of the C# projection is written from the IR, as the import stubs are, since a second reader
of the header could disagree with the IR and would not see its annotations. What .NET adds is
`LibraryImport` with the library name, accessors for bit-fields, and a C shim built with the
package that exports the `static inline` functions and call-like macros P/Invoke cannot reach.

**D108. The platform's C compiler builds for the host; `zig cc` is the provider of cross
compilation**, because it carries the C libraries of every target in one download. A user who
never cross-compiles never needs Zig, and only one provider knows about it, so it can be replaced
by clang with sysroots or by toolchain packs. `Debug` builds of the host on `x86_64` Linux compile with tcc, first in
`auto`: compiling the generated C is the largest share of a rebuild, tcc does it an order of
magnitude faster, and it optimizes nothing, so `ReleaseSafe` keeps the optimizing compilers.

**D109. The project file is an MSBuild SDK-style `.csproj` using `CDelta.Sdk`.** IDEs open it, NuGet
distributes packages, MSBuild orders builds. The target is the property `CDeltaTarget`: MSBuild reserves `Target`, the names .NET
already uses (`RuntimeIdentifier`, `PlatformTarget`) carry other meanings, and the `CDelta`
prefix, as in `CDeltaEdition`, marks a property no other SDK defines.

**D110. Compilation is whole-program from sources.** Packages ship sources plus a reference
assembly for the IDE.

**D111. The same analyzer runs in the IDE and in `cdc`.**

**D112. `cdc` runs out of process from MSBuild.** MSBuild carries its own Roslyn.

**D113. The compiler is deterministic: identical inputs give identical C.**

**D114. The IR has one text format, printed and parsed, used for tests and dumps.**

**D115. kabic becomes the C frontend's tag reader plus projections, then is archived.**

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
the documents is answered by a change to the documents, and the affected work waits for it.

**D128. `stackalloc` has only its pointer form.** Roslyn converts `stackalloc` to a span only when
the span is a ref struct, and spans as fields of ordinary structs (D009 and the slice model) matter
more than that shorthand. A span over stack memory is `new Span<T>(p, n)`; constant data on the
stack uses collection expressions.

**D129. The prelude takes from libc only two functions, in their own translation unit.**
`cd_libc_write_stderr` and `cd_libc_abort` live in `<PackageName>.libc.c`; the rest of the prelude
is plain C and builtins. A `[DllImport]` whose name an imported header declares uses the header's
declaration.

**D130. A build from files has the directory of the first file as its package root.** Display
paths of its sources are relative to it, with `..` segments when a file is outside it.

**D131. `ref`, `in`, `out` and `this` are pointer-typed parameters with a mode in the IR.** The
IR makes every indirection explicit; the mode remains for diagnostics and projections.

**D132. Pointer arithmetic and comparison are `binop`s on pointers, scaled as in C, with no
check.**

**D133. Every IR array value is a wrapper struct in C**, so arrays assign and return; only
imported structs keep C array fields.

**D134. Bit-fields record their bit offset.** Comptime and projections need the exact position;
the C compiler still lays them out and the catalog test compares bit offsets with clang.

**D135. Misaligned explicit field offsets are valid and packed in C; references to such fields
are rejected (`CD2008`).** C headers contain packed layouts, and a misaligned reference would be
illegal behavior that is always wrong.

**D136. `const` declarations take any visibility; every field is public.**

**D137. Stack memory of constant size outside loops is a function-wide array local; otherwise an
`alloca` rvalue.** Each `stackalloc` evaluation gets new memory, as in C# (D156); a collection
expression reuses one storage, as Roslyn's inline array does. `alloca` names its initialization
so Debug can fill it.

**D138. Slicing is one IR rvalue over a slice or array place.** It never copies an array, gives
the core one place to check bounds, and replaces the `array.slice` cast.

**D139. An optional's payload is variant `0`, and every read of it is checked in safe modes.**
The core does no flow analysis to elide checks.

**D140. The generated C silences GCC's built-in declaration mismatch in the file itself.**
`[DllImport]` prototypes keep the IR's types, the ABI is the same, and the C file stays
compilable without toolchain flags.

**D141. The development environment grows as the documents need.** A missing tool is added to
`eng/setup-dev.sh`; tests and checks are never weakened to fit what the container has.

**D142. No garbage collection or automatic reference counting in the language or the std.**
Automatic strategies need hidden calls or a runtime, and their explicit versions remove no work.
Arenas and pools with handles remove the work instead, and are the std's answer; a conservative
collector can only be a package, behind `IAllocator`.

**D143. User types declare arithmetic, bitwise and comparison operators and explicit
conversions.** An operator on a user type is visibly an operation on that type, receives nothing
but its operands, and returns a value, never a `Result`: it can neither allocate behind the
caller's back nor fail silently. Forbidding operators would force every numeric type (vectors,
matrices, fixed point, colors) into the compiler as intrinsics, which does not scale. Implicit
conversions stay removed because nothing in the text shows the call; `true`, `false`, `++`, `--`
and compound assignment operators stay removed to keep one spelling.

**D144. `delegate*<...>` is the C function pointer.** CΔ has no managed code, so C#'s managed
and unmanaged function pointers mean the same thing; the plain form needs no
`[UnmanagedCallersOnly]` on callbacks. The unmanaged form remains only to name a non-default
calling convention.

**D145. Errors are symbolic values in the IR; enums become errors through casts by member name.**
The frontend cannot know the program-wide numbers, and an enum's declared values are not error
codes, so the IR names errors until the core numbers them. A `result` is read with the same
projections as an optional, and every read is checked in safe modes, leaving the redundant test
after a branch to the C compiler.

**D146. No borrow checker; memory safety help is opt-in and lives outside the compiler.** A
borrow checker needs move semantics and lifetime syntax that C# lacks, and an optional one
proves little. Roslyn's ref safety on `ref struct` types gives checked views for free, an
analyzer package gives lints, and the debug allocator and pools catch the rest at run time.

**D147. The std grows by projecting C libraries that meet CΔ's rules.** Projection lets a small
std close the largest gaps left by the BCL with mature code. A library qualifies only with a
permissive license, allocation through per-context hooks, no hidden global state, errors as
return values and reentrancy; otherwise it gives nothing over `[Import]`. Modules needed at
comptime, on freestanding targets or generically stay CΔ.

**D148. `string` is UTF-8 text: a corelib struct of pointer and length.** Text is too common for
`ReadOnlySpan<byte>` and free functions; Roslyn accepts a struct `System.String`, so CΔ keeps C#'s
own name, literals, `==`, `switch` and `foreach` with no class, no GC and no hidden allocation.
Bytes are reached through `Bytes` and `Slice`, never through C#'s `Length` and indexers (D156); characters are `Rune`, and
`char` remains only for constants. Positions and counts in text live in `s.Bytes`, never on
`string`: C# counts UTF-16 units and CΔ UTF-8 ones, so any accented letter would make the same
member give another number. Members that expose no unit (`StartsWith`, `Split`, `Trim`, `==`)
behave as in C#, and `EnumerateRunes()` keeps its BCL name and result.

**D149. Std types take the BCL's names when they keep its meaning.** `Dictionary`, `HashSet`,
`Queue`, `Stack`, `StringBuilder`, `Random`, `Stopwatch` are structs over explicit allocators, but
a C# programmer finds them where expected (D002).

**D150. User indexers have a getter only, by value or by `ref`.** `x[i]` reads as element access
of a type the reader sees, like an operator; a setter would hide insertion, and insertion
allocates. A collection whose C# indexer setter inserts, as `Dictionary`, gets a value getter, so
the assignment does not compile.

**D151. Iterators are struct state machines generated by the frontend.** They give lazy sequences
without allocation; the result stays a concrete value because an interface value would need
boxing.

**D152. Concurrency is a library: an `IIo` interface passed like an allocator.** Code written
against it runs blocking, on threads or evented, as its caller chooses, so functions are not
colored. In order: `Std.Io` with a thread pool backend, no compiler work; an evented backend over
fixed-size stacks (a fixed stack needs no garbage collector, a growing one does); and only if
still needed, `async` and `await` as the iterator transform, with struct task types, frames as
values that never move once started, and allocation only at spawn. No runtime, no hidden
allocation. Tasks are structured: a `Task` lives in a `using` declaration, which cancels and
waits on every exit, the error returns of `Try` included, and `CD2002` keeps a value holding a
capturing lambda in its frame, so tasks capture freely with no collector. A task that is not
waited takes a `static` lambda and a copied state; a task's slot, taken at spawn from the
`IIo`'s allocator, is its one allocation. The std has the whole set under the .NET names
(threads, atomics, locks, tasks, data parallelism, channels, concurrent collections), with only
the `IIo` parameter and `Result` new; the primitives sit on the OS, and schedulers, deques,
channels and queues are CΔ. Evented stacks are carved from one reservation of address space and
committed on touch, as the virtual arena does (D174); guard pages are on by default and can be
turned off for scale, since each one splits the mapping the kernel counts. Scheduling steals by
default and is per core for servers, and io_uring is used in batches with registered buffers, which
is where servers in C gain their speed, not in the language. `CD1012` and `CD1015`
name what to write instead of `async` and `lock`.

**D153. A struct may implement getter-only static abstract properties.** Generic math names
static values as properties (`T.Zero`, `AdditiveIdentity`); without them a user type could not
implement the numeric interfaces. A static property is not read as a field of a value, so the
reason properties are removed does not apply; instance properties stay removed.

**D154. LINQ is a std module of lazy struct queries.** Each operator is a struct over its source,
so a chain is one value on the stack with no interface and no boxing; only the terminals that
need memory take an allocator. Query syntax stays removed, one spelling per meaning. `CD2002`
follows a capturing lambda into the values that hold it, so a query cannot escape its frame.

**D155. Top-level statements are kept.** Short programs and scripts are a real use, as `cdc run
file.cs` shows. C# makes them return only `void` or `int`, so `Try` goes through
`Std.Process.Run`, and the implicit `args` array is rejected.

**D156. No silent divergence from C#.** A kept construct behaves as in C#, or the difference
stops compilation; at run time CΔ differs only where C# would throw, by panicking. A familiar
name with a different behavior is worse than a missing one. So `string` has no byte `Length`,
`Dictionary` has no inserting indexer, integer to enum conversions are unchecked, `stackalloc`
gets new memory per evaluation, loop captures that would differ are rejected (`CD2009`), and the
default arithmetic context wraps when checks are off instead of being assumed not to overflow.

**D157. CΔ can do anything C can, at worst through an explicit form.** Not being specialized
means no closed door: freestanding code, vendor HALs, interrupt handlers, inline assembly, SIMD,
linker control. Every capability is listed in [language/c-parity.md](language/c-parity.md); a
missing one is a design gap.

**D158. Build speed is a requirement, measured in CI.** A warm compiler server, the generated C
split by module with cached objects, and a NativeAOT `cdc` remove most of the latency of a
rebuild. If the spike shows that MSBuild is what keeps the .NET SDK required, a reader for the
subset of the project file that [language/modules-build.md](language/modules-build.md) defines,
falling back to MSBuild for anything else, is a candidate; a build language of CΔ's own is not.

**D159. Debugging shows CΔ, not the generated C.** The IR knows every type, error name and union
member, so the build writes formatters for GDB, LLDB and Visual Studio from it.

**D160. Hot reload and the DOM are planned directions.** Both fit without a runtime: reloading a
shared library whose state lives in its host, and a projection for JavaScript glue. Hot
reload is part of M8, because it decides whether CΔ is a good language for game code.

**D161. Editions per package, meeting in Delta IR.** CΔ follows C# as it evolves without
splitting its ecosystem: each package keeps the C# version and CΔ rules it was written for, the
compiler supports every past edition, and packages of different editions form one program
because each is its own Roslyn compilation. CΔ's own rules change incompatibly only at a new
edition, with code fixes to migrate; the std only grows. 1.0 comes after months of real use, not
after a milestone, and is a reset: nothing from the 0.x releases is carried into it. Editions
follow every C# version, with no preview and no long-term edition, because staying current is
the point; the compiler runs on a long-term .NET (D202), which is independent of the C# version it
binds.

**D162. CΔ packages depend on each other as source; there is no CΔ ABI.** Monomorphized
generics, comptime and cross-package inlining need the bodies, and CΔ's own types have no C form
worth freezing. Binary distribution goes through the C ABI and the CΔ projection; the
object cache keeps rebuilding dependencies cheap.

**D163. Toolchains are installed per target on request, from a pinned list.** Mature cross
toolchains exist per target (wasi-sdk, the Arm GNU toolchain, llvm-mingw); `cdc toolchain install`
fetches the right one by hash, so no single toolchain is a dependency of CΔ.

**D164. Dependencies come from NuGet or from a URL with a hash.** NuGet carries published CΔ
packages with versions, a package type and lock files; `CDeltaFetch` carries anything else, as
`zig fetch` and CMake's `FetchContent` do: an archive or a git commit fixed by its hash, built
as a CΔ package or by a recipe for C. A C library becomes a one-line dependency without being
republished.

**D165. A CΔ library is a plain C library to C consumers.** Binary outputs come with CMake and
`pkg-config` files; the C source output comes with a `CMakeLists.txt`, so a C project uses a CΔ
library with no CΔ tooling installed.

**D166. CΔ evolves from kabic; it does not port it.** kabic was designed for another context, so
each of its pieces is evaluated against CΔ's design and redesigned or dropped. Its code and
tests are a source of cases, and neither its output nor its manifest format binds CΔ.

**D167. Projections can be plugins in any language.** As `protoc` does, `cdc` hands the
declarations and analyses to an external program over standard input and writes what it returns,
so each language's community writes its own backend in its own language.

**D168. The declaration layer of the IR is the stable interface; bodies are internal.** Plugins
and projections need declarations; nothing outside the compiler builds bodies yet, so freezing
them would only slow the compiler down.

**D169. Delta IR knows no language runtime; a frontend brings its own.** Reaching everything C
reaches is enough to express any runtime written in C, so a language with a collector or
exceptions compiles through the IR with its runtime's sources, as Nim compiles its runtime with
its programs. The IR gains only what runtimes need from C (frame addresses, guaranteed tail
calls, assembly), never a model of one language.

**D170. Annotations are language-neutral facts on IR declarations.** What enriches a C header
for C# today enriches any frontend's declarations for any backend: the vocabulary is defined on
IR concepts, and a manifest names declarations by IR key, so neither end is tied to C or to C#. The vocabulary comes from a clean room aimed at the CΔ
projection: projections of real APIs written by hand without kabic's output, each fact becoming
an annotation only if it also means something in C#; how a language shows a fact is policy of its
projection, never IR. CΔ comes first because CΔ consumes it, and the C# projection is its shape
with the std swapped for the BCL, so code over a library looks alike in both.

**D171. The IR grows by need, always lowerable to C, with dialects for single languages.** Higher
constructs save frontends from repeating lowerings and keep meaning for comptime and projections,
but each one costs every component of the compiler; so none enters without a frontend that needs
it, a C lowering and an interpreter semantics.

**D172. C++ and other languages are consumed through C, never through their own ABI.** A
generated `extern "C"` shim lets the C++ compiler resolve mangling, layout and exceptions; an
importer of a language's own interface metadata turns its C ABI into rich declarations. Either
way the result is ordinary IR declarations, so every backend and plugin serves them.

**D173. CΔ's ecosystem is every library reachable through the C ABI.** A new language needs
years to grow an ecosystem; CΔ inherits all of them by wrapping instead of rewriting. Its own
work concentrates on what wrapping cannot give: annotations and projections that make a foreign
library feel native.

**D174. The std has a virtual arena.** Reserving address space and committing it as it is used
gives a buffer that grows without moving and a scope freed in one store, with no new language
concept: it is one more `IAllocator`.

**D175. Flat data with offsets instead of pointers is a std library, later.** Offsets make data
copyable, position independent and serializable with one copy; they need generics and `unsafe`
only, so they wait for a program that needs them.

**D176. Comptime reflection walks fields and methods.** A loop over a comptime-only value is
unrolled, the folding of conditions extended to loops, so code that runs at run time walks a type
field by field with each description a constant. Methods bind to delegates through
`As<TDelegate>()`, whose type is a type argument, so C# type checking holds; the compiler checks
that the signatures agree, and a `void*` parameter adapted by a thunk lets a table dispatch over
methods whose types it does not know. Finding behaviours by name or attribute is comptime's, not
a generator's.

## Repository

**D201. Documents and code are in English.**

**D202. The compiler targets .NET 10 and Roslyn pinned for C# 14.**

**D203. The documents describe the design, never the state of the work.**
