static class A { static void Main() { } }
static class B { static int Main() { return 0; } }
static class C { static Result Main() { return default; } }
static class D { static Result<int> Main() { return 0; } }
static class E { static long Main() { return 0; } }
static class F { static void Main(int argc) { } }
static class G { static Result<long> Main() { return 0L; } }
