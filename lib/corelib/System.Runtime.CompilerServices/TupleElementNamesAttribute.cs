using System;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Event, Inherited = false)]
public sealed class TupleElementNamesAttribute : Attribute
{
    public TupleElementNamesAttribute(string[] transformNames)
    {
    }
}
