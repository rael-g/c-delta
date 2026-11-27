using System.Numerics;

namespace System;

public struct Int64 : INumber<long>
{
    public const long MinValue = -9223372036854775807L - 1;
    public const long MaxValue = 9223372036854775807;

    static long IAdditionOperators<long,long,long>.operator +(long left, long right) => throw null;

    static long ISubtractionOperators<long,long,long>.operator -(long left, long right) => throw null;

    static long IMultiplyOperators<long,long,long>.operator *(long left, long right) => throw null;

    static long IDivisionOperators<long,long,long>.operator /(long left, long right) => throw null;

    static bool IEqualityOperators<long, long, bool>.operator ==(long left, long right) => throw null;

    static bool IEqualityOperators<long, long, bool>.operator !=(long left, long right) => throw null;

    static bool IComparisonOperators<long, long, bool>.operator <(long left, long right) => throw null;

    static bool IComparisonOperators<long, long, bool>.operator >(long left, long right) => throw null;

    static bool IComparisonOperators<long, long, bool>.operator <=(long left, long right) => throw null;

    static bool IComparisonOperators<long, long, bool>.operator >=(long left, long right) => throw null;

    static long IAdditiveIdentity<long, long>.AdditiveIdentity => throw null;

    static long IMultiplicativeIdentity<long, long>.MultiplicativeIdentity => throw null;

    static long INumberBase<long>.One => throw null;

    static long INumberBase<long>.Zero => throw null;
}
