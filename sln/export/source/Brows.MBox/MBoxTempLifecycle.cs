using Brows.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class MBoxTempLifecycle : IExportAndInit, IExportAndKill {
    Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        MBoxTemp.Clean();
        return Task.CompletedTask;
    }

    void IExportAndKill.Kill() {
        MBoxTemp.Shutdown();
    }
}
