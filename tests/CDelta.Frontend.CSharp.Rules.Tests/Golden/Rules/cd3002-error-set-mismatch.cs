enum Small { A }
enum Big { A, B }

static class P
{
    static Result<int, Big> Source() { return Big.B; }

    static Result<int, Small> Narrow()
    {
        return Source().Try();
    }

    static Result<int, Big> Wide(Result<int, Small> r)
    {
        return r.Try();
    }

    static Result<int> Any()
    {
        return Source().Try();
    }
}
