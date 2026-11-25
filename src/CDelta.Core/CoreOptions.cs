using CDelta.Ir;

namespace CDelta.Core;

public readonly record struct CoreOptions(BuildMode Mode, Target Target, bool? ErrorReturnTrace = null)
{
    public bool TracesErrors => ErrorReturnTrace ?? Mode is BuildMode.Debug or BuildMode.ReleaseSafe;
}
