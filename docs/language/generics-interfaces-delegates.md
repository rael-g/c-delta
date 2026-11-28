# Generics, interfaces and delegates

## Generics

Generic structs, interfaces, delegates and methods keep C# syntax and type checking. They differ
from C# in one way that has no visible effect on a correct program: they are always
**monomorphized**. Each distinct set of type arguments produces its own compiled copy, and every
call through a constraint is resolved statically.

- Type checking happens once, on the generic definition, under C# rules: a generic body may only
  use what its constraints allow. This is stricter than duck-typed compile-time generics and it is
  what keeps CΔ valid C#.
- Constraints: interfaces, `struct`, `unmanaged`, `new()`, `allows ref struct`, and type
  parameters. `class`, `class?`, `notnull` and base class constraints cannot be satisfied and are
  rejected by `CD1000`.
- Static abstract and static virtual interface members are kept. They are the way to call a
  function of `T` without a value: `T.Zero`, `T.Parse(text)`. A static abstract property has a
  getter only, and a struct implements it with a `static` getter-only property, which is the one
  property user code may declare (D153):

  ```csharp
  struct Vector2 : IAdditionOperators<Vector2, Vector2, Vector2>, IAdditiveIdentity<Vector2, Vector2>
  {
      public float X, Y;
      public static Vector2 AdditiveIdentity => default;
      public static Vector2 operator +(Vector2 a, Vector2 b) => new() { X = a.X + b.X, Y = a.Y + b.Y };
  }
  ```
- Generic math works on primitives and user types: the corelib declares `INumber<T>` and the
  operator interfaces (`IAdditionOperators<T, T, T>` and the others of C#, with their static
  properties such as `Zero` and `AdditiveIdentity`), implemented by every primitive, and a struct
  that declares the operators and those properties may implement them, so one `Lerp<T>` serves
  `float` and a `Vector2`.
- Inside an instantiation, `typeof(T) == typeof(int)` and `value is int i` on a `T` are folded at
  compile time, so generic code can specialize per type with no cost. The type tested is a
  concrete type; testing a `T` against an interface or a `[Union]` is `CD1000`.
- Instantiation depth is limited to 64 nested generic instantiations (`CD5005`), which catches
  unbounded recursion like `F<T>` calling `F<List<T>>`.
- Static fields of generic types exist once per instantiation, as in C#.

## Interfaces

An interface is a contract: method signatures, static abstract members, and optionally default
method bodies. Interfaces may extend other interfaces. They declare no properties except
getter-only static abstract ones, no indexer setters and no events (`CD1004` to `CD1006`); they may declare indexer getters and static abstract
operators of the kept set.

A struct implements an interface by listing it, as in C#. There are three ways to use one.

### 1. As a constraint: static dispatch

```csharp
static void WriteAll<TWriter>(ref TWriter writer, ReadOnlySpan<byte> data) where TWriter : IWriter
    => writer.Write(data);
```

Zero cost: the call resolves to the concrete struct's method at instantiation, and may be inlined.
This is the default choice.

### 2. As a value: dynamic dispatch

```csharp
var arena = new ArenaAllocator(backing);
IAllocator allocator = Dyn<IAllocator>.Of(ref arena);
```

An interface value is a **fat pointer**: the address of the implementing value and the address of
a static table of its functions, one table per (struct, interface) pair. Calls go through the
table.

- It is made only with `Dyn<I>.Of(ref x)`, which makes its reference nature explicit. `I` must be
  an interface that is not `[Union]` (`CD5006`). The implicit
  C# conversion from a struct to an interface would copy into a heap box, so it is rejected
  (`CD5001`).
- It does not own the value. Its lifetime is the lifetime of `x`.
- `default` and `null` are the null interface value; calling it is illegal behavior.
- An interface with static abstract members cannot be a value type of a variable, as in C#.
- Default interface method bodies are compiled once per implementing struct. As in C#, a generic
  call on a struct that reaches a default method runs it on a copy of the struct, so a mutation it
  makes through `this` is lost; through a `Dyn<I>` or a `[Union]` value it changes the struct held.

### 3. As a tagged union: `[Union]`

```csharp
[Union] interface Shape { }
struct Circle : Shape { public float Radius; }
struct Rect : Shape { public float Width; public float Height; }

static float Area(Shape shape) => shape switch
{
    Circle c => MathF.PI * c.Radius * c.Radius,
    Rect r => r.Width * r.Height,
};
```

A `[Union]` interface is a closed set of structs. A value of its type holds one of them **by
value**: a tag and storage for the largest member, with the largest alignment.

- Converting a struct to it copies the struct in, which is exactly the C# meaning of converting a
  struct to an interface (a copy), without the heap.
- Type patterns (`is Circle c`, `case Rect r`) test the tag and copy the payload out, again with the
  C# meaning.
- The implementing structs must be declared in the same package, must not be generic, and must not
  be `ref struct` (`CD5002`). That makes the set known, so `switch` over a union must cover every
  member or have a discard arm (`CD2001`).
- A `[Union]` interface may declare methods. Calling one switches on the tag.
- Type patterns are allowed only on `[Union]` values and on type parameters (`CD5004`).

## Delegates

A delegate type is a **value**: a function pointer and a context pointer, two pointers wide. It is
not an object, it is never multicast, and it never allocates.

```csharp
delegate int Compare(int a, int b);

static void Sort(Span<int> items, Compare compare) { ... }
```

- Delegate declarations, `Func<...>` and `Action<...>` (corelib) are kept.
- Delegate values come from lambdas, local functions and static method groups.
- A method group of a struct instance method (`list.Append`) is rejected (`CD5007`): in C# it
  would copy the struct into a box. Write a lambda that captures the variable.
- Invoking a delegate calls the function with the context. Invoking a null delegate is illegal
  behavior.
- `+`, `-`, `+=`, `-=` and `Delegate.Combine` do not exist (`CD1029`).
- Because Roslyn considers delegates reference types, a struct with a delegate field is managed in
  C#: it cannot be pointed to with `T*`. Use `ref`.

### Function pointers

`delegate*<int, int, int>` is a bare C function pointer with the target's default calling
convention. Every CΔ function already has the C ABI, so there is no managed form: a static method
or a `static` lambda converts to it with `&Method` and can be handed to C as a callback, with no
attribute.

```csharp
static void OnKey(int key) { }

C.set_key_callback(&OnKey);
```

`delegate* unmanaged[Stdcall]<...>` and the other named conventions exist for C functions that
state a convention other than the default. As in C#, `&Method` to such a pointer requires
`[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]` on the method, which gives the
method that convention. `delegate* unmanaged<...>` with no named convention is the default
convention and is written `delegate*<...>` instead (`CD1030`).

## Lambdas and closures

```csharp
int threshold = 10;
var count = Count(items, x => x > threshold);
```

- A lambda that captures nothing, or a `static` lambda, compiles to a plain function. Its delegate
  has a null context.
- A capturing lambda captures **variables, not values**, as in C#. The compiler moves the captured
  locals of the enclosing function into a frame struct on the stack, and the delegate's context
  points to it. Mutation through either side is visible to both, as in C#.
- The frame lives as long as the enclosing function call. A capturing lambda must not outlive it.
  A value that holds one, directly or in a field at any depth, is treated like the lambda itself,
  and the compiler rejects the direct escapes of either (`CD2002`): returning it, storing it in a
  static, writing it through a pointer, a `ref` or an `out` parameter. Storing it in a field of a
  local, as a lazy query does, and passing it as an argument are allowed; the callee storing it is
  the programmer's responsibility.
- In C#, each iteration of a loop has its own copy of a variable declared in the loop body,
  including the `foreach` variable, so lambdas made in different iterations see different
  variables. CΔ keeps one frame per call, so it rejects what would differ (`CD2009`): a lambda
  that captures a variable declared inside a loop must not be stored in anything declared outside
  that loop. Calling it, or passing it as an argument, inside the iteration is allowed.
- Lambdas passed to `Defer`, `ErrDefer` and `Comptime` are not delegates: they are inlined or
  evaluated, and none of the above applies.
- Local functions follow the same rules as lambdas. A `static` local function is a nested
  function.
