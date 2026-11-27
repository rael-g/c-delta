using System.Numerics;

namespace System;

public struct Int32 : INumber<int>
{
    public const int MinValue = -2147483647 - 1;
    public const int MaxValue = 2147483647;

    static int IAdditionOperators<int,int,int>.operator +(int left, int right) => throw null;

    static int ISubtractionOperators<int,int,int>.operator -(int left, int right) => throw null;

    static int IMultiplyOperators<int,int,int>.operator *(int left, int right) => throw null;

    static int IDivisionOperators<int,int,int>.operator /(int left, int right) => throw null;

    static bool IEqualityOperators<int, int, bool>.operator ==(int left, int right) => throw null;

    static bool IEqualityOperators<int, int, bool>.operator !=(int left, int right) => throw null;

    static bool IComparisonOperators<int, int, bool>.operator <(int left, int right) => throw null;

    static bool IComparisonOperators<int, int, bool>.operator >(int left, int right) => throw null;

    static bool IComparisonOperators<int, int, bool>.operator <=(int left, int right) => throw null;

    static bool IComparisonOperators<int, int, bool>.operator >=(int left, int right) => throw null;

    static int IAdditiveIdentity<int, int>.AdditiveIdentity => throw null;

    static int IMultiplicativeIdentity<int, int>.MultiplicativeIdentity => throw null;

    static int INumberBase<int>.One => throw null;

    static int INumberBase<int>.Zero => throw null;
}
