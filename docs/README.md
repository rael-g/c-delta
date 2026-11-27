# Documentation

Read in this order. Each document is normative for its area; when two documents disagree, the
more specific one wins and the other is a bug to fix.

## Foundations

1. [vision.md](vision.md): what CΔ is, the north, the principles and the non-goals.
2. [decisions.md](decisions.md): every design decision with its reason, numbered.

## The language

3. [language/overview.md](language/overview.md): a tour and the mental model.
4. [language/subset.md](language/subset.md): every C# feature, kept, restricted or removed.
5. [language/types.md](language/types.md): primitives, pointers, slices, arrays, structs, enums,
   unions, optionals, text, conversions and numeric semantics.
6. [language/errors.md](language/errors.md): error sets, `Result`, `Try`, `ErrDefer`, panics.
7. [language/memory.md](language/memory.md): allocation, `Defer`, lifetimes, build modes and
   illegal behavior.
8. [language/generics-interfaces-delegates.md](language/generics-interfaces-delegates.md):
   monomorphized generics, the three uses of an interface, delegates and lambdas.
9. [language/concurrency.md](language/concurrency.md): the `IIo` model, structured tasks, channels,
   threads, and the forms of C#'s `async`.
10. [language/comptime.md](language/comptime.md): code that runs in the compiler.
11. [language/intrinsics-attributes.md](language/intrinsics-attributes.md): the full list of
    intrinsics and attributes, with signatures.
12. [language/c-interop.md](language/c-interop.md): importing C, extern functions, exporting.
13. [language/modules-build.md](language/modules-build.md): namespaces, packages, visibility,
    projects, targets, conditional compilation, tests.
14. [language/c-parity.md](language/c-parity.md): every capability of C and its form in CΔ.

## The compiler

15. [compiler/architecture.md](compiler/architecture.md): components, dependencies, pipeline.
16. [compiler/ir.md](compiler/ir.md): Delta IR.
17. [compiler/frontend-csharp.md](compiler/frontend-csharp.md): Roslyn, the subset checker and
    lowering.
18. [compiler/frontend-c.md](compiler/frontend-c.md): reading C through libclang.
19. [compiler/core-passes.md](compiler/core-passes.md): instantiation, comptime, safety, layout.
20. [compiler/backend-c.md](compiler/backend-c.md): generating C11.
21. [compiler/driver-toolchain-sdk.md](compiler/driver-toolchain-sdk.md): `cdc`, the C toolchain,
    the MSBuild SDK and the IDE.
22. [compiler/diagnostics.md](compiler/diagnostics.md): the registry of every diagnostic code.
23. [compiler/testing.md](compiler/testing.md): how everything is tested.
24. [compiler/engineering.md](compiler/engineering.md): environment, packages, project settings,
    code style, test infrastructure, Roslyn probes and CI.

## Libraries and the future

25. [std.md](std.md): the standard library.
26. [kabic-migration.md](kabic-migration.md): how kabic becomes a frontend and backends of CΔ.
27. [roadmap.md](roadmap.md): milestones and their acceptance criteria.
