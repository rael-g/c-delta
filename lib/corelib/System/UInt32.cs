using System.Numerics;

namespace System;

public struct UInt32 : INumber<uint>
{
    public const uint MinValue = 0;
    public const uint MaxValue = 4294967295;

    static uint IAdditionOperators<uint,uint,uint>.operator +(uint left, uint right) => throw null;

    static uint ISubtractionOperators<uint,uint,uint>.operator -(uint left, uint right) => throw null;

    static uint IMultiplyOperators<uint,uint,uint>.operator *(uint left, uint right) => throw null;

    static uint IDivisionOperators<uint,uint,uint>.operator /(uint left, uint right) => throw null;

    static bool IEqualityOperators<uint, uint, bool>.operator ==(uint left, uint right) => throw null;

    static bool IEqualityOperators<uint, uint, bool>.operator !=(uint left, uint right) => throw null;

    static bool IComparisonOperators<uint, uint, bool>.operator <(uint left, uint right) => throw null;

    static bool IComparisonOperators<uint, uint, bool>.operator >(uint left, uint right) => throw null;

    static bool IComparisonOperators<uint, uint, bool>.operator <=(uint left, uint right) => throw null;

    static bool IComparisonOperators<uint, uint, bool>.operator >=(uint left, uint right) => throw null;

    static uint IAdditiveIdentity<uint, uint>.AdditiveIdentity => throw null;

    static uint IMultiplicativeIdentity<uint, uint>.MultiplicativeIdentity => throw null;

    static uint INumberBase<uint>.One => throw null;

    static uint INumberBase<uint>.Zero => throw null;
}
