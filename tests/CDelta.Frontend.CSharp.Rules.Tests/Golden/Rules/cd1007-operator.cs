struct S
{
    public static S operator +(S a, S b) { return a; }
    public static explicit operator int(S s) { return 0; }
    public static implicit operator long(S s) { return 0; }
    public static S operator ++(S a) { return a; }
    public static bool operator true(S a) { return true; }
    public static bool operator false(S a) { return false; }
}
