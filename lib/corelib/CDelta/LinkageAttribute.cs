using System;

namespace CDelta;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
public sealed class LinkageAttribute : Attribute
{
    public LinkageAttribute(LinkageKind kind)
    {
    }
}
