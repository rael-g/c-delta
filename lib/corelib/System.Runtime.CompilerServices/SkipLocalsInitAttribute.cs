namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, Inherited = false)]
public sealed class SkipLocalsInitAttribute : Attribute
{
}
