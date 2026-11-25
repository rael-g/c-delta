using System;

namespace CDelta;

[AttributeUsage(AttributeTargets.All)]
public sealed class UnsupportedAttribute : Attribute
{
    public UnsupportedAttribute(string reason)
    {
    }
}
