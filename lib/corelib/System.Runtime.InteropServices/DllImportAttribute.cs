using System;

namespace System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DllImportAttribute : Attribute
{
    public string EntryPoint;
    public CharSet CharSet;
    public bool SetLastError;
    public bool PreserveSig;
    public bool BestFitMapping;
    public bool ThrowOnUnmappableChar;
    public CallingConvention CallingConvention;

    public DllImportAttribute(string dllName)
    {
    }

    public string Value => null;
}
