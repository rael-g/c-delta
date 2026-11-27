namespace System.Numerics;

public interface INumber<TSelf> : INumberBase<TSelf>, IComparisonOperators<TSelf, TSelf, bool>
    where TSelf : INumber<TSelf>
{
}
