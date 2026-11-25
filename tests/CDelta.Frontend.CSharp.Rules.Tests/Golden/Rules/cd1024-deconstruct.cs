struct S
{
    public int A;
    public int B;
    public void Deconstruct(out int a, out int b) { a = A; b = B; }
}
