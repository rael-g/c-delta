using System.Numerics;

namespace System;

public struct UInt64 : INumber<ulong>
{
    public const ulong MinValue = 0;
    public const ulong MaxValue = 18446744073709551615;

    static ulong IAdditionOperators<ulong,ulong,ulong>.operator +(ulong left, ulong right) => throw null;

    static ulong ISubtractionOperators<ulong,ulong,ulong>.operator -(ulong left, ulong right) => throw null;

    static ulong IMultiplyOperators<ulong,ulong,ulong>.operator *(ulong left, ulong right) => throw null;

    static ulong IDivisionOperators<ulong,ulong,ulong>.operator /(ulong left, ulong right) => throw null;

    static bool IEqualityOperators<ulong, ulong, bool>.operator ==(ulong left, ulong right) => throw null;

    static bool IEqualityOperators<ulong, ulong, bool>.operator !=(ulong left, ulong right) => throw null;

    static bool IComparisonOperators<ulong, ulong, bool>.operator <(ulong left, ulong right) => throw null;

    static bool IComparisonOperators<ulong, ulong, bool>.operator >(ulong left, ulong right) => throw null;

    static bool IComparisonOperators<ulong, ulong, bool>.operator <=(ulong left, ulong right) => throw null;

    static bool IComparisonOperators<ulong, ulong, bool>.operator >=(ulong left, ulong right) => throw null;

    static ulong IAdditiveIdentity<ulong, ulong>.AdditiveIdentity => throw null;

    static ulong IMultiplicativeIdentity<ulong, ulong>.MultiplicativeIdentity => throw null;

    static ulong INumberBase<ulong>.One => throw null;

    static ulong INumberBase<ulong>.Zero => throw null;
}
