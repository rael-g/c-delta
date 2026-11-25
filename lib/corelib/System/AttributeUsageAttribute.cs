namespace System;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AttributeUsageAttribute : Attribute
{
    public AttributeUsageAttribute(AttributeTargets validOn)
    {
    }

    public bool AllowMultiple { get; set; }

    public bool Inherited { get; set; }
}
