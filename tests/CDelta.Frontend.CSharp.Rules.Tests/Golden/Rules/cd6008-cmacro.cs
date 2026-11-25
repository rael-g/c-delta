static unsafe class C
{
    [CMacro] public static extern int isdigit(int c);

    [CMacro] public static int notExtern(int c) { return c; }
}

struct S
{
    [CMacro] public extern int notStatic(int c);
}
