using System.Numerics;

namespace System;

public struct Single : INumber<float>
{
    public const float NaN = 0.0f / 0.0f;
    public const float PositiveInfinity = 1.0f / 0.0f;
    public const float NegativeInfinity = -1.0f / 0.0f;

    static float IAdditionOperators<float,float,float>.operator +(float left, float right) => throw null;

    static float ISubtractionOperators<float,float,float>.operator -(float left, float right) => throw null;

    static float IMultiplyOperators<float,float,float>.operator *(float left, float right) => throw null;

    static float IDivisionOperators<float,float,float>.operator /(float left, float right) => throw null;

    static bool IEqualityOperators<float, float, bool>.operator ==(float left, float right) => throw null;

    static bool IEqualityOperators<float, float, bool>.operator !=(float left, float right) => throw null;

    static bool IComparisonOperators<float, float, bool>.operator <(float left, float right) => throw null;

    static bool IComparisonOperators<float, float, bool>.operator >(float left, float right) => throw null;

    static bool IComparisonOperators<float, float, bool>.operator <=(float left, float right) => throw null;

    static bool IComparisonOperators<float, float, bool>.operator >=(float left, float right) => throw null;

    static float IAdditiveIdentity<float, float>.AdditiveIdentity => throw null;

    static float IMultiplicativeIdentity<float, float>.MultiplicativeIdentity => throw null;

    static float INumberBase<float>.One => throw null;

    static float INumberBase<float>.Zero => throw null;
}
