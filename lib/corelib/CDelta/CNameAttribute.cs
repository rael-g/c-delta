using System;

namespace CDelta;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Enum)]
public sealed class CNameAttribute : Attribute
{
    public CNameAttribute(string name)
    {
    }
}
