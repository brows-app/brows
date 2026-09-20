using Brows.Composition;

namespace Brows;

internal sealed class ImportSandboxEnvironment : IImportEnvironment {
    public ImportInfo ImportInfo { get; init; }
}
