using System.Numerics;

namespace System;

public struct Byte : INumber<byte>
{
    public const byte MinValue = 0;
    public const byte MaxValue = 255;

    static byte IAdditionOperators<byte,byte,byte>.operator +(byte left, byte right) => throw null;

    static byte ISubtractionOperators<byte,byte,byte>.operator -(byte left, byte right) => throw null;

    static byte IMultiplyOperators<byte,byte,byte>.operator *(byte left, byte right) => throw null;

    static byte IDivisionOperators<byte,byte,byte>.operator /(byte left, byte right) => throw null;

    static bool IEqualityOperators<byte, byte, bool>.operator ==(byte left, byte right) => throw null;

    static bool IEqualityOperators<byte, byte, bool>.operator !=(byte left, byte right) => throw null;

    static bool IComparisonOperators<byte, byte, bool>.operator <(byte left, byte right) => throw null;

    static bool IComparisonOperators<byte, byte, bool>.operator >(byte left, byte right) => throw null;

    static bool IComparisonOperators<byte, byte, bool>.operator <=(byte left, byte right) => throw null;

    static bool IComparisonOperators<byte, byte, bool>.operator >=(byte left, byte right) => throw null;

    static byte IAdditiveIdentity<byte, byte>.AdditiveIdentity => throw null;

    static byte IMultiplicativeIdentity<byte, byte>.MultiplicativeIdentity => throw null;

    static byte INumberBase<byte>.One => throw null;

    static byte INumberBase<byte>.Zero => throw null;
}
