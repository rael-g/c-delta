# CΔ (C Delta)

CΔ is a compiled systems language. Its syntax and semantics are a subset of C#, it reads and
exports C natively, and it is designed by the philosophy of explicit, simple, allocation-aware
systems programming.

- No garbage collector, no JIT, no VM, no CLR, no mandatory runtime.
- No classes, inheritance, polymorphism or encapsulation.
- Structs, interfaces, generics, delegates, lambdas and attributes.
- Errors are values (error unions); there are no exceptions.
- Comptime: code that runs inside the compiler.
- C headers are imported directly; CΔ code is exported with a C ABI and a generated header.

The compiler uses Roslyn as its C# frontend, lowers to its own IR (Delta IR), and generates C11.
Frontend, core and backend are separate, so other frontends (C) and other backends (bindings for
C# and Zig) plug into the same IR.

Start at [docs/README.md](docs/README.md).
