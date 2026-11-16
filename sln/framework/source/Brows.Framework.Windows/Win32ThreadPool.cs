using Brows.Composition;
using Brows.Threading;
using System;

namespace Brows;

internal sealed class Win32ThreadPool : ISTAThreadPoolProvider, IExportAndKill {
    private readonly Lazy<ISTAThreadPool> LazyCommon;

    private ISTAThreadPool Common => field ??= LazyCommon.Value;

    [ImportRequired]
    internal ISTAThreadPoolFactory ThreadPoolFactory { get; set; }

    internal Win32ThreadPool() {
        LazyCommon = new(() => {
            return ThreadPoolFactory?.CreateThreadPool(nameof(Win32ThreadPool))
                ?? throw new InvalidOperationException("Unable to create the common thread pool!");
        });
    }

    ISTAThreadPool ISTAThreadPoolProvider.GetThreadPool(object target) {
        return Common;
    }

    void IExportAndKill.Kill() {
        Common.Empty();
    }
}
