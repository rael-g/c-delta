namespace CDelta.Conformance.Tests;

internal sealed record ConformanceCase(string RelativePath, string Mode, string Target)
{
    public override string ToString() => RelativePath + " [" + Mode + ", " + Target + "]";
}
