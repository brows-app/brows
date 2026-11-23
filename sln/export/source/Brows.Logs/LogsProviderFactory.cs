using System.Threading.Tasks;

namespace Brows;

internal sealed class LogsProviderFactory : ProviderFactory<LogsProvider> {
    internal const string ProviderID = "Logs";

    protected sealed override Task<LogsProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
        if (id != ProviderID) {
            return Task.FromResult<LogsProvider>(null);
        }
        return Task.FromResult(new LogsProvider(id, Translation));
    }

    public ITranslationExport Translation { get; set; }
}
