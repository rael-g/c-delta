namespace CDelta.Toolchain;

public readonly record struct ArchiveRequest(IReadOnlyList<string> Objects, string Output, string Target);
