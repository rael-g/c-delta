namespace System.Numerics;

public interface INumberBase<TSelf> :
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IMultiplyOperators<TSelf, TSelf, TSelf>,
    IDivisionOperators<TSelf, TSelf, TSelf>,
    IEqualityOperators<TSelf, TSelf, bool>,
    IAdditiveIdentity<TSelf, TSelf>,
    IMultiplicativeIdentity<TSelf, TSelf>
    where TSelf : INumberBase<TSelf>
{
    static abstract TSelf One { get; }

    static abstract TSelf Zero { get; }
}
