using Domore.Logs;
using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

internal sealed class ConfigObjectProvider : IConfigObjectProvider, IExportAndKill {
    private static readonly ILog Log = Logging.For(typeof(ConfigObjectProvider));

    private readonly SemaphoreSlim Locker = new(1, 1);
    private readonly Dictionary<Type, object> Cache = [];

    private async Task<T> Create<T>(CancellationToken token) where T : new() {
        /*
         * Make sure the configuration exists or the default configuration
         * has been written.
         */
        await Config.Read<T>(token);
        /*
         * Read the configuration object and watch for changes.
         */
        return await Config.Watch<T>(
            token: token,
            errored: ex => {
                if (Log.Warn()) {
                    Log.Warn($"Errored > {typeof(T)}",
                             ex);
                }
            },
            configured: () => {
                if (Log.Debug()) {
                    Log.Debug($"Configured > {typeof(T)}");
                }
            });
    }

    internal IConfigDefault Config { get; set; }

    async Task<T> IConfigObjectProvider.Get<T>(CancellationToken token) {
        await Locker.WaitAsync(token);
        try {
            if (Cache.TryGetValue(typeof(T), out var value) == false) {
                Cache[typeof(T)] = value = await Create<T>(token);
            }
            return (T)value;
        }
        finally {
            Locker.Release();
        }
    }

    void IExportAndKill.Kill() {
        Locker.Dispose();
    }
}
