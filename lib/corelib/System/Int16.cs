using System.Numerics;

namespace System;

public struct Int16 : INumber<short>
{
    public const short MinValue = -32768;
    public const short MaxValue = 32767;

    static short IAdditionOperators<short,short,short>.operator +(short left, short right) => throw null;

    static short ISubtractionOperators<short,short,short>.operator -(short left, short right) => throw null;

    static short IMultiplyOperators<short,short,short>.operator *(short left, short right) => throw null;

    static short IDivisionOperators<short,short,short>.operator /(short left, short right) => throw null;

    static bool IEqualityOperators<short, short, bool>.operator ==(short left, short right) => throw null;

    static bool IEqualityOperators<short, short, bool>.operator !=(short left, short right) => throw null;

    static bool IComparisonOperators<short, short, bool>.operator <(short left, short right) => throw null;

    static bool IComparisonOperators<short, short, bool>.operator >(short left, short right) => throw null;

    static bool IComparisonOperators<short, short, bool>.operator <=(short left, short right) => throw null;

    static bool IComparisonOperators<short, short, bool>.operator >=(short left, short right) => throw null;

    static short IAdditiveIdentity<short, short>.AdditiveIdentity => throw null;

    static short IMultiplicativeIdentity<short, short>.MultiplicativeIdentity => throw null;

    static short INumberBase<short>.One => throw null;

    static short INumberBase<short>.Zero => throw null;
}
