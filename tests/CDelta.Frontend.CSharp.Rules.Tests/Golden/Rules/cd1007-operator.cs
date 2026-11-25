struct S
{
    public static S operator +(S a, S b) { return a; }
    public static implicit operator int(S s) { return 0; }
}
