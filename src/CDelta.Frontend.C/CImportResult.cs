using CDelta.Ir;

namespace CDelta.Frontend.C;

public sealed record CImportResult(Module Module, IReadOnlyList<ImportedMacro> Macros, IReadOnlyDictionary<string, string> Unsupported);
