# CΔ

CΔ is a compiled language whose syntax and semantics are a subset of C#, that reads and exports C,
and follows the philosophy of explicit, allocation-aware systems programming. The compiler uses
Roslyn as its frontend, Delta IR as its core, and generates C11.

## The north

When a question is not answered by `docs/`, decide by: the philosophy of explicit systems
programming (no hidden control flow, no hidden allocation, no runtime, errors as values, comptime),
the syntax and semantics of C#, and interoperability with C. Then record the decision in
`docs/decisions.md`. See `docs/vision.md`.

## Invariants

- A CΔ program is valid C#. Never add syntax; remove, or use an intrinsic or attribute.
- Before adding an intrinsic, look for a C# name that already has the meaning.
- Frontends, core and backends are separate projects. A backend references only `CDelta.Ir` and
  `CDelta.Diagnostics`. A frontend never references the core or a backend.
- Nothing of C# enters the IR. Roslyn symbols are translated at the frontend boundary.
- The subset checker is an allowlist.
- Lazy by design: when a library or tool solves a problem (Roslyn, libclang, the C compiler,
  MSBuild, NuGet, a C library), use it instead of reimplementing it.
- Portable projects (see `docs/compiler/architecture.md`) are written in the data-oriented style
  listed there, because they will be ported to CΔ.
- Every diagnostic code is registered in `docs/compiler/diagnostics.md` and in
  `CDelta.Diagnostics`; a code is never reused.
- The documents in `docs/` are normative. A change of behavior changes its document in the same
  commit.

## Layout

- `src/`: compiler projects, one per component (see `docs/compiler/architecture.md`).
- `sdk/CDelta.Sdk/`: the MSBuild SDK.
- `lib/corelib/`: declarations Roslyn binds against.
- `lib/std/`: the standard library, in CΔ.
- `tests/`: unit, conformance and end to end tests.
- `samples/`: example programs.
- `docs/`: the design; `docs/kanban/` holds status and is not versioned.

## Commands

```bash
dotnet build CDelta.slnx
dotnet test CDelta.slnx
tests/conformance/run.sh
tests/e2e/run.sh
```

Tools run in the distrobox `dev`: `distrobox enter dev -- bash -lc 'cd ~/Projetos/c-delta && ...'`.

## Conventions

- No comments in source. A doc comment states what a caller relies on; never history, context or
  state.
- A magic number becomes a named constant, never a commented literal.
- No em dash in any text.
- No reference to third-party work in code, comments, documents or credits.
- Conventional Commits, one line, one logical task per commit, staged selectively. No
  `Co-Authored-By`.
- Every commit builds alone.
- Code and documents are in English; answer the owner in Brazilian Portuguese.
- Release builds and archives go to `~/Downloads`, never into the repository.
