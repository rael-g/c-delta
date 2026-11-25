using System;

namespace System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class StructLayoutAttribute : Attribute
{
    public int Pack;
    public int Size;

    public StructLayoutAttribute(LayoutKind layoutKind)
    {
    }
}
