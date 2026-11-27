namespace System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method)]
public sealed class UnmanagedCallersOnlyAttribute : Attribute
{
    public Type[] CallConvs;
}
