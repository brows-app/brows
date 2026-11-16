using System.Threading.Tasks;

namespace Brows.Commands;

internal abstract class FtpDelete<TProvider> : FtpCommand<TProvider, FtpDelete<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    private static readonly TimeSpan FailedOperationRefreshTimeout = TimeSpan.FromSeconds(5);

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var panel) ||
            !panel.HasProvider<TProvider>(out var provider) ||
            !context.HasSource(out FtpEntry _, out var entries)) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var didDelete = false;
            Exception operationFailure = null;
            try {
                foreach (var entry in entries) {
                    token.ThrowIfCancellationRequested();
                    await provider.DeleteRecursive(entry, _ => didDelete = true, token);
                }
                token.ThrowIfCancellationRequested();
                return didDelete;
            }
            catch (Exception exception) {
                operationFailure = exception;
                throw;
            }
            finally {
                if (didDelete) {
                    if (operationFailure is null) {
                        await Refresh(provider, token);
                    }
                    else {
                        using var refreshCancellation = new CancellationTokenSource(FailedOperationRefreshTimeout);
                        try {
                            await Refresh(provider, refreshCancellation.Token);
                        }
                        catch (Exception) when (operationFailure is not null) {
                        }
                    }
                }
            }
        });
    }

    protected virtual Task Refresh(TProvider provider, CancellationToken token) {
        return ((IProvider)provider).Refresh(token);
    }

    public sealed class Parameter {
    }
}
