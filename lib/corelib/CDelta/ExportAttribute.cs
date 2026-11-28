using System;

namespace CDelta;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field)]
public sealed class ExportAttribute : Attribute
{
    public ExportAttribute()
    {
    }

    public ExportAttribute(string name)
    {
    }
}
