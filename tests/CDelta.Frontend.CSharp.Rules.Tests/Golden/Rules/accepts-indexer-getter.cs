struct Grid
{
    public int Width;

    public int this[int x] => x * Width;
}

unsafe struct Cells
{
    public int* First;

    public ref int this[int i] => ref *First;
}
