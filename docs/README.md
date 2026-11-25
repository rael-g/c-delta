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
9. [language/comptime.md](language/comptime.md): code that runs in the compiler.
10. [language/intrinsics-attributes.md](language/intrinsics-attributes.md): the full list of
    intrinsics and attributes, with signatures.
11. [language/c-interop.md](language/c-interop.md): importing C, extern functions, exporting.
12. [language/modules-build.md](language/modules-build.md): namespaces, packages, visibility,
    projects, targets, conditional compilation, tests.

## The compiler

13. [compiler/architecture.md](compiler/architecture.md): components, dependencies, pipeline.
14. [compiler/ir.md](compiler/ir.md): Delta IR.
15. [compiler/frontend-csharp.md](compiler/frontend-csharp.md): Roslyn, the subset checker and
    lowering.
16. [compiler/frontend-c.md](compiler/frontend-c.md): reading C through libclang.
17. [compiler/core-passes.md](compiler/core-passes.md): instantiation, comptime, safety, layout.
18. [compiler/backend-c.md](compiler/backend-c.md): generating C11.
19. [compiler/driver-toolchain-sdk.md](compiler/driver-toolchain-sdk.md): `cdc`, the C toolchain,
    the MSBuild SDK and the IDE.
20. [compiler/diagnostics.md](compiler/diagnostics.md): the registry of every diagnostic code.
21. [compiler/testing.md](compiler/testing.md): how everything is tested.
22. [compiler/engineering.md](compiler/engineering.md): environment, packages, project settings,
    code style, test infrastructure, Roslyn probes and CI.

## Libraries and the future

23. [std.md](std.md): the standard library.
24. [kabic-migration.md](kabic-migration.md): how kabic becomes a frontend and backends of CΔ.
25. [roadmap.md](roadmap.md): milestones and their acceptance criteria.

Work status lives in `docs/kanban/`, which is not versioned.
