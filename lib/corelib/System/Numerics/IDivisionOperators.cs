namespace System.Numerics;

public interface IDivisionOperators<TSelf, TOther, TResult>
    where TSelf : IDivisionOperators<TSelf, TOther, TResult>
{
    static abstract TResult operator /(TSelf left, TOther right);
}
