namespace CDelta;

using System;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ImportAttribute : Attribute
{
    public ImportAttribute(params string[] headers)
    {
    }

    public string[] Defines { get; set; }

    public string[] IncludeDirs { get; set; }

    public string[] Only { get; set; }
}
