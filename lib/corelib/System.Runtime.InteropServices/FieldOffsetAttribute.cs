using System;

namespace System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class FieldOffsetAttribute : Attribute
{
    public FieldOffsetAttribute(int offset)
    {
    }
}
