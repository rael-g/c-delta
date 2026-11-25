using System;

namespace CDelta;

[AttributeUsage(AttributeTargets.Field)]
public sealed class BitsAttribute : Attribute
{
    public BitsAttribute(int width)
    {
    }
}
