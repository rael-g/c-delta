namespace System.Numerics;

public interface ISubtractionOperators<TSelf, TOther, TResult>
    where TSelf : ISubtractionOperators<TSelf, TOther, TResult>
{
    static abstract TResult operator -(TSelf left, TOther right);
}
