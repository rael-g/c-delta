# kabic migration

kabic (`~/Projetos/kabic`) reads C headers and their doc tags and describes them idiomatically in
C# and Zig. Its pipeline is a compiler: a C reader, a model, a classifier, writers. CΔ is its
evolution, with a richer model and more frontends and backends, so kabic is discontinued once CΔ
does its job.

CΔ does not port kabic (D166). kabic was designed for another context, and it adapts to CΔ, never
the reverse. Each piece below is a candidate: it is evaluated against CΔ's design, then redesigned
on CΔ's terms or discarded. kabic's code, tests and end to end suites are a source of cases and
of hard-won knowledge, not a specification, and matching kabic's output is not a goal.

## The doctrine, as a starting point

kabic's doctrine is the starting point of the C frontend and of every projection backend, kept
where it holds in CΔ:

- The C code already has semantics; what the AST says is read, never restated by a tag.
- Tags add only what C cannot say, state facts about a declaration, and never name a consumer.
- Writers spell semantics in their language and hold no other logic.
- A header that needs a tag to rebuild something is often the header to fix.
- Additional logic belongs in extensions that live with whoever needs them.
- The generic output is the goal, never a reproduction of earlier output.

## Candidates

Each row is evaluated before it is built; the right column is where the piece would land if it
survives.

| kabic | CΔ |
|---|---|
| `Kabic.Reader`: clang AST, doc tags, YAML annotations, constants | `CDelta.Frontend.C`: reading on libclang; tags and the annotation manifest as `ann.*` annotations ([compiler/ir.md](compiler/ir.md#annotations)) |
| `Kabic.Core`: `ApiModel`, `CType` | Delta IR declarations; `CType` is the IR type with C spelling |
| `Kabic.Core`: the slot classifier (owner, bag, vtable, factory) | `CDelta.Projection`: shared analyses over IR declarations used by every projection backend |
| `Kabic.Core`: `Convention` (name rules and type maps per language) | projection backend options, in a format designed for `cdc` |
| `Kabic.CSharp` writer | `CDelta.Backend.CSharp`, bindings mode |
| `Kabic.Zig` writer | `CDelta.Backend.Zig` |
| `Kabic.Bindings` (raw bindings through ClangSharp) | the raw layer of `CDelta.Backend.CSharp`, still through ClangSharp |
| `Kabic.Pipeline`, domains, regeneration, drift | `cdc project` command and its domain manifest |
| `Kabic.Factories` and other extensions | extensions loaded by `cdc project`, outside the core repository |
| checks (reconstruction, API coverage) | `cdc project --check` |

`CDelta.Projection` and the projection backends depend only on `CDelta.Ir`, like every backend.

## The command

```bash
cdc project --manifest api_domains.json --lang csharp --out src/csharp
cdc project --manifest api_domains.json --lang zig --out src/zig
cdc project --manifest api_domains.json --check
```

A projection backend may also live outside the compiler, in any language, as a plugin (D167):

```bash
cdc project --manifest api.json --plugin ./cdelta-python --out src/python
```

`cdc` runs the plugin, writes to its standard input one request holding the declaration layer
of the IR (declarations, attributes and annotations), the analyses of `CDelta.Projection` and the
plugin's options, and reads from its standard output the files to write and any diagnostics. The
request and the response are JSON with a format version; their schema is published with the
compiler. A community writes the backend for its language in that language, without touching
CΔ's repository.

The manifest format is designed for `cdc`; kabic's format is not read. A one-time converter
from kabic's manifests may exist if it is cheaper than rewriting them.

## What CΔ gains

- CΔ imports can use the same tags: a projection backend for CΔ itself turns an `array_of` pair
  into a `Span<T>` overload and an `out` pointer into an `out` parameter, on top of the raw import.
  This is optional and layered; the raw import of [language/c-interop.md](language/c-interop.md)
  stays the default.
- CΔ exports go through the same projection backends, so a library written in CΔ gets C#, Zig and
  CΔ bindings of its exported surface from the same tool.

## Order

1. CΔ reaches milestone M5 (C import) of the [roadmap](roadmap.md): the C frontend reads
   declarations, layouts, constants and comments.
2. Review every candidate above against CΔ's design and record what is kept, changed or
   dropped, before any of it is built; the review is a gap review like those of the milestones.
3. Build the annotations of the C frontend, `CDelta.Projection` and the Zig and C# backends from
   that review, taking kabic's tests as cases where they still apply.
4. Run the end to end suites (probe, zlib, sqlite, vulkan) through `cdc project`. They must
   compile and pass their runtime checks; their output is CΔ's, not kabic's.
5. Switch KernelEngine to `cdc project`. Until then kabic stays untouched and in use.
6. Archive kabic.

No change is made in the kabic repository during the migration; it keeps serving its consumers
until step 6 succeeds.
