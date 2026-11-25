# kabic migration

kabic (`~/Projetos/kabic`) reads C headers and their doc tags and describes them idiomatically in
C# and Zig. Its pipeline is a compiler: a C reader, a model, a classifier, writers. CΔ already
needs the reader and a model richer than kabic's; kabic therefore becomes a C frontend feature and
a set of projection backends of CΔ, and is then discontinued.

## What carries over unchanged

kabic's doctrine is the doctrine of the C frontend and of every projection backend:

- The C code already has semantics; what the AST says is read, never restated by a tag.
- Tags add only what C cannot say, state facts about a declaration, and never name a consumer.
- Writers spell semantics in their language and hold no other logic.
- A header that needs a tag to rebuild something is often the header to fix.
- Additional logic belongs in extensions that live with whoever needs them.
- The generic output is the goal, never a reproduction of earlier output.

## Mapping

| kabic | CΔ |
|---|---|
| `Kabic.Reader`: clang AST, doc tags, YAML annotations, constants | `CDelta.Frontend.C`: the same responsibilities on libclang, tags as `c.tag.*` IR attributes |
| `Kabic.Core`: `ApiModel`, `CType` | Delta IR declarations; `CType` is the IR type with C spelling |
| `Kabic.Core`: the slot classifier (owner, bag, vtable, factory) | `CDelta.Projection`: shared analyses over IR declarations used by every projection backend |
| `Kabic.Core`: `Convention` (name rules and type maps per language) | projection backend options, in the same YAML shape |
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

The manifest format is kabic's, read as is, so consumers change the command, not their files.

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
2. Port the tag reader and the YAML annotations into the C frontend, with kabic's tests for them.
3. Build `CDelta.Projection` and port the classifier, with kabic's shape tests.
4. Port the Zig writer, then the C# writer, each with its shape tests.
5. Run kabic's end to end suites (probe, zlib, sqlite, vulkan) through `cdc project`. Outputs may
   differ from kabic's where the difference is semantically equal; they must compile and pass the
   same runtime checks.
6. Switch KernelEngine to `cdc project`. Until then kabic stays untouched and in use.
7. Archive kabic.

No change is made in the kabic repository during the migration; it keeps serving its consumers
until step 6 succeeds.
