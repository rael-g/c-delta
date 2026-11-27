using Std;

interface IAllocator
{
    Result<Span<byte>> Alloc(nuint size, nuint alignment);
}

enum AllocError { OutOfMemory }

unsafe struct ArenaAllocator : IAllocator
{
    public byte* Base;
    public nuint Capacity;
    public nuint Used;

    public Result<Span<byte>> Alloc(nuint size, nuint alignment)
    {
        nuint start = (Used + alignment - 1) / alignment * alignment;
        if (start + size > Capacity)
        {
            return Fail(AllocError.OutOfMemory);
        }

        Used = start + size;
        return new Span<byte>(Base + start, (int)size);
    }
}

unsafe struct List<T> where T : unmanaged
{
    private const int FirstCapacity = 4;
    private const nuint Alignment = 8;

    public IAllocator Allocator;
    public T* Items;
    public int Count;
    public int Capacity;

    public Result Append(T value)
    {
        if (Count == Capacity)
        {
            int capacity = Capacity == 0 ? FirstCapacity : Capacity * 2;
            Span<byte> memory = Allocator.Alloc((nuint)(capacity * sizeof(T)), Alignment).Try();
            T* grown;
            fixed (byte* raw = memory)
            {
                grown = (T*)raw;
            }

            for (int i = 0; i < Count; i++)
            {
                grown[i] = Items[i];
            }

            Items = grown;
            Capacity = capacity;
        }

        Items[Count] = value;
        Count++;
        return default;
    }
}

static unsafe class Program
{
    private const int ArenaSize = 512;

    static Result<int> Main()
    {
        byte* storage = stackalloc byte[ArenaSize];
        var arena = new ArenaAllocator { Base = storage, Capacity = ArenaSize };
        IAllocator allocator = Dyn<IAllocator>.Of(ref arena);

        List<int> numbers = default;
        numbers.Allocator = allocator;
        List<long> wide = default;
        wide.Allocator = allocator;
        for (int i = 1; i <= 9; i++)
        {
            numbers.Append(i).Try();
            wide.Append(i * 2L).Try();
        }

        int total = 0;
        for (int i = 0; i < numbers.Count; i++)
        {
            total += numbers.Items[i] + (int)wide.Items[i];
        }

        return total - 93;
    }
}
