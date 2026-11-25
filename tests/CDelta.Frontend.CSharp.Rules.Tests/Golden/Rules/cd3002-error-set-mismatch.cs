enum Small { A }
enum Big { A, B }

static class P
{
    static Result<int, Big> Source() { return Big.B; }

    static Result<int, Small> Narrow()
    {
        return Try(Source());
    }

    static Result<int, Big> Wide(Result<int, Small> r)
    {
        return Try(r);
    }

    static Result<int> Any()
    {
        return Try(Source());
    }
}
