using System.Numerics;

namespace System;

public struct SByte : INumber<sbyte>
{
    public const sbyte MinValue = -128;
    public const sbyte MaxValue = 127;

    static sbyte IAdditionOperators<sbyte,sbyte,sbyte>.operator +(sbyte left, sbyte right) => throw null;

    static sbyte ISubtractionOperators<sbyte,sbyte,sbyte>.operator -(sbyte left, sbyte right) => throw null;

    static sbyte IMultiplyOperators<sbyte,sbyte,sbyte>.operator *(sbyte left, sbyte right) => throw null;

    static sbyte IDivisionOperators<sbyte,sbyte,sbyte>.operator /(sbyte left, sbyte right) => throw null;

    static bool IEqualityOperators<sbyte, sbyte, bool>.operator ==(sbyte left, sbyte right) => throw null;

    static bool IEqualityOperators<sbyte, sbyte, bool>.operator !=(sbyte left, sbyte right) => throw null;

    static bool IComparisonOperators<sbyte, sbyte, bool>.operator <(sbyte left, sbyte right) => throw null;

    static bool IComparisonOperators<sbyte, sbyte, bool>.operator >(sbyte left, sbyte right) => throw null;

    static bool IComparisonOperators<sbyte, sbyte, bool>.operator <=(sbyte left, sbyte right) => throw null;

    static bool IComparisonOperators<sbyte, sbyte, bool>.operator >=(sbyte left, sbyte right) => throw null;

    static sbyte IAdditiveIdentity<sbyte, sbyte>.AdditiveIdentity => throw null;

    static sbyte IMultiplicativeIdentity<sbyte, sbyte>.MultiplicativeIdentity => throw null;

    static sbyte INumberBase<sbyte>.One => throw null;

    static sbyte INumberBase<sbyte>.Zero => throw null;
}
