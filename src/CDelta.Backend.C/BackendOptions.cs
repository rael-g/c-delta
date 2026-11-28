namespace CDelta.Backend.C;

public readonly record struct BackendOptions(bool LineDirectives, bool LinkLibC, bool SharedLibrary = false);
