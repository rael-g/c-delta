namespace System.Numerics;

public interface IMultiplyOperators<TSelf, TOther, TResult>
    where TSelf : IMultiplyOperators<TSelf, TOther, TResult>
{
    static abstract TResult operator *(TSelf left, TOther right);
}
