namespace CDelta.Frontend.CSharp.Tests.Probes;

public sealed class CorelibProbeTests
{
    [Fact]
    public void EmptyModule() => CorelibCompilation.AssertBinds("static class M { }");

    [Fact]
    public void StringToSpan() => CorelibCompilation.AssertBinds("""
        static class M { static void F() { ReadOnlySpan<byte> s = "abc"; } }
        """);

    [Fact]
    public void StringToPointer() => CorelibCompilation.AssertBinds("""
        static unsafe class M { static void F() { byte* p = "abc"; } }
        """);

    [Fact]
    public void SpanAsAField() => CorelibCompilation.AssertBinds("""
        struct S { public Span<int> Items; }
        static unsafe class M { static void F(S* s) { } }
        """);

    [Fact]
    public void Stackalloc() => CorelibCompilation.AssertBinds("""
        static unsafe class M { static void F() { int* p = stackalloc int[4]; var s = new Span<int>(p, 4); } }
        """);

    [Fact]
    public void StackallocToSpanIsRejected()
    {
        IReadOnlyList<string> errors = CorelibCompilation.Errors("""
            static class M { static void F() { Span<int> s = stackalloc int[4]; } }
            """);

        Assert.Single(errors);
        Assert.Contains("CS8346", errors[0]);
    }

    [Fact]
    public void CollectionExpression() => CorelibCompilation.AssertBinds("""
        static class M { static void F() { Span<int> s = [1, 2, 3]; } }
        """);

    [Fact]
    public void SpanIterationAndRanges() => CorelibCompilation.AssertBinds("""
        static class M
        {
            static void F(Span<int> s)
            {
                foreach (var x in s) { }
                var a = s[1..];
                var b = s[^1];
            }
        }
        """);

    [Fact]
    public void ResultConversions() => CorelibCompilation.AssertBinds("""
        enum E { A }
        static class M
        {
            static Result<int, E> F() => 1;
            static Result<int, E> G() => E.A;
        }
        """);

    [Fact]
    public void DeferAndTry() => CorelibCompilation.AssertBinds("""
        enum E { A }
        static class M
        {
            static void G() { }
            static Result<int, E> F() => 1;
            static Result<int, E> H()
            {
                Defer(() => G());
                var v = Try(F());
                return v;
            }
        }
        """);

    [Fact]
    public void Panic() => CorelibCompilation.AssertBinds("""
        static class M { static int F() { throw Panic("x"); } }
        """);

    [Fact]
    public void VariadicExtern() => CorelibCompilation.AssertBinds("""
        static unsafe class M
        {
            static extern int printf(byte* f, __arglist);
            static void G(byte* f) { printf(f, __arglist(1, 2)); }
        }
        """);

    [Fact]
    public void ExternWithoutAttribute() => CorelibCompilation.AssertBinds("""
        static class M { static extern int f(int x); }
        """);

    [Fact]
    public void AttributeArrays() => CorelibCompilation.AssertBinds("""
        [Import("a.h", Defines = ["X"])]
        static partial class C { }
        """);
}
