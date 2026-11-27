using System.Numerics;

namespace System;

public struct UInt16 : INumber<ushort>
{
    public const ushort MinValue = 0;
    public const ushort MaxValue = 65535;

    static ushort IAdditionOperators<ushort,ushort,ushort>.operator +(ushort left, ushort right) => throw null;

    static ushort ISubtractionOperators<ushort,ushort,ushort>.operator -(ushort left, ushort right) => throw null;

    static ushort IMultiplyOperators<ushort,ushort,ushort>.operator *(ushort left, ushort right) => throw null;

    static ushort IDivisionOperators<ushort,ushort,ushort>.operator /(ushort left, ushort right) => throw null;

    static bool IEqualityOperators<ushort, ushort, bool>.operator ==(ushort left, ushort right) => throw null;

    static bool IEqualityOperators<ushort, ushort, bool>.operator !=(ushort left, ushort right) => throw null;

    static bool IComparisonOperators<ushort, ushort, bool>.operator <(ushort left, ushort right) => throw null;

    static bool IComparisonOperators<ushort, ushort, bool>.operator >(ushort left, ushort right) => throw null;

    static bool IComparisonOperators<ushort, ushort, bool>.operator <=(ushort left, ushort right) => throw null;

    static bool IComparisonOperators<ushort, ushort, bool>.operator >=(ushort left, ushort right) => throw null;

    static ushort IAdditiveIdentity<ushort, ushort>.AdditiveIdentity => throw null;

    static ushort IMultiplicativeIdentity<ushort, ushort>.MultiplicativeIdentity => throw null;

    static ushort INumberBase<ushort>.One => throw null;

    static ushort INumberBase<ushort>.Zero => throw null;
}
