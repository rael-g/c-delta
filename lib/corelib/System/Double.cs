using System.Numerics;

namespace System;

public struct Double : INumber<double>
{
    public const double NaN = 0.0 / 0.0;
    public const double PositiveInfinity = 1.0 / 0.0;
    public const double NegativeInfinity = -1.0 / 0.0;

    static double IAdditionOperators<double,double,double>.operator +(double left, double right) => throw null;

    static double ISubtractionOperators<double,double,double>.operator -(double left, double right) => throw null;

    static double IMultiplyOperators<double,double,double>.operator *(double left, double right) => throw null;

    static double IDivisionOperators<double,double,double>.operator /(double left, double right) => throw null;

    static bool IEqualityOperators<double, double, bool>.operator ==(double left, double right) => throw null;

    static bool IEqualityOperators<double, double, bool>.operator !=(double left, double right) => throw null;

    static bool IComparisonOperators<double, double, bool>.operator <(double left, double right) => throw null;

    static bool IComparisonOperators<double, double, bool>.operator >(double left, double right) => throw null;

    static bool IComparisonOperators<double, double, bool>.operator <=(double left, double right) => throw null;

    static bool IComparisonOperators<double, double, bool>.operator >=(double left, double right) => throw null;

    static double IAdditiveIdentity<double, double>.AdditiveIdentity => throw null;

    static double IMultiplicativeIdentity<double, double>.MultiplicativeIdentity => throw null;

    static double INumberBase<double>.One => throw null;

    static double INumberBase<double>.Zero => throw null;
}
