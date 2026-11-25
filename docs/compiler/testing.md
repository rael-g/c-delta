# Testing

## Principles

- Every rule in the language documents has at least one test that accepts a correct program and,
  for a restriction, one that rejects a wrong one with the exact diagnostic code.
- Tests compare behavior, not internal shapes, except at the IR and C boundaries where golden
  files are the contract.
- A generated C file that does not compile is a test failure even if no test looked at it.
- Expectations live in files next to the test input, never in comments of the input.

## Layers

| Layer | Project | What |
|---|---|---|
| Unit | `tests/CDelta.Ir.Tests` | builder, verifier, printer and parser round trip |
| Unit | `tests/CDelta.Core.Tests` | each pass on IR written in the text format; the interpreter |
| Unit | `tests/CDelta.Frontend.CSharp.Tests` | lowering of each construct to IR, golden |
| Unit | `tests/CDelta.Frontend.CSharp.Rules.Tests` | each analyzer rule, accepted and rejected sources |
| Unit | `tests/CDelta.Frontend.C.Tests` | headers to IR declarations and stubs, golden |
| Unit | `tests/CDelta.Backend.C.Tests` | IR to C, golden, and compiled |
| Unit | `tests/CDelta.Driver.Tests` | project loading, option mapping, toolchain discovery with fakes |
| Conformance | `tests/conformance` | the language, end to end |
| End to end | `tests/e2e` | real programs and real C libraries |
| Std | `lib/std` | the std's own `[Test]` functions, run by `cdc test` |

Unit tests use xunit. Golden files are updated with `CDELTA_UPDATE_GOLDEN=1` and reviewed in the
diff like code.

## Conformance suite

```
tests/conformance/
  errors/
    try-propagates.cs
    try-propagates.stdout
    try-incompatible-set.cs
    try-incompatible-set.diagnostics
  ...
```

| Sidecar | Meaning |
|---|---|
| `.stdout` | the program builds, runs, and prints exactly this |
| `.exitcode` | the expected exit code, `0` when absent |
| `.diagnostics` | the build fails with exactly these `path(line,col): code` lines |
| `.panic` | the program panics with this message (safe modes only) |
| `.modes` | the build modes to run in, all four when absent |
| `.targets` | the targets to run on, the host when absent |

The runner is an xunit theory over the directory, one case per file and mode. Directories mirror
the language documents (`types`, `errors`, `memory`, `generics`, `comptime`, `interop`, `subset`).

## Differential tests

- **Comptime against run time.** For every conformance program marked `.differential`, its
  functions are evaluated both by the interpreter and by the compiled program, and the results
  must be byte-identical. This keeps the interpreter honest about target semantics.
- **Modes.** Programs with no illegal behavior must print the same in all four modes.
- **Toolchains.** Tier 1 targets build the conformance suite with `zig cc`, Clang and GCC; outputs
  must be identical.

## Strict C

CI compiles all generated C of the conformance suite with `-std=c11 -Wall -Wextra -Werror
-pedantic` (extensions allowed only behind feature tests) and runs it under UBSan and ASan in
Debug mode. A sanitizer report on a program without illegal behavior is a backend bug.

## Layouts

`tests/CDelta.Core.Tests` computes layouts for a catalog of structs (alignment, padding, packing,
explicit offsets, bit-fields) for every tier 1 and 2 target and compares them with clang's answers
for equivalent C structs, generated on the fly. The static assertions in generated C repeat the
check on every real build.

## End to end

| Case | Proves |
|---|---|
| `hello` | the smallest program on every tier 1 target |
| `freestanding` | a program with no libc, built for `x86_64-freestanding-none`, linked with a custom start |
| `export-lib` | a CΔ static and shared library used by a C program through the generated header |
| `zlib`, `sqlite` | importing real headers, linking real libraries, round trips of data |
| `vulkan-headers` | importing a large header (types, enums, function pointers) without running it |
| `cross` | building `hello` for every tier 1 and 2 target from one host |
| `sdk` | `dotnet build`, `dotnet run`, `dotnet test` and `dotnet pack` on a sample project |
| `ide` | a design-time build produces the import stubs and analyzer diagnostics |

The C libraries come from the system or from the cases' own `native/` directories, as in kabic's
end to end tests.

## Commands

```bash
dotnet build CDelta.slnx
dotnet test CDelta.slnx
tests/conformance/run.sh [--mode Debug] [--filter errors/]
tests/e2e/run.sh
```

All run in the distrobox `dev`.
