using Brows.Programs;
using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public sealed class WindowsProgram : Program<WindowsProgramConfig> {
    private static readonly string LockName = LockNameFactory();

    private static string LockNameFactory() {
        using (var identity = WindowsIdentity.GetCurrent()) {
            var sid = identity.User?.Value ?? identity.Name.Replace('\\', '.');
            /*
             * Global namespace so that the same user gets one instance across all sessions
             * (console, RDP, fast user switching), qualified by SID so that each user gets
             * their own.
             */
            return $"Global\\Brows.WindowsProgram.{sid}";
        }
    }

    private static async Task<T> Lock<T>(Func<bool, Task<T>> func) {
        ArgumentNullException.ThrowIfNull(func);
        /*
         * Semaphore rather than Mutex: a Mutex is owned by the thread that acquired it and
         * must be released by that same thread, but the continuation after the await below
         * resumes on an arbitrary thread pool thread.
         *
         * If this process exits without releasing, it is the only handle holder, so the
         * kernel destroys the object and the next launch starts from a full count.
         */
        using (var semaphore = new Semaphore(initialCount: 1, maximumCount: 1, name: LockName)) {
            var owned = semaphore.WaitOne(TimeSpan.Zero);
            try {
                return await func(owned);
            }
            finally {
                if (owned) {
                    semaphore.Release();
                }
            }
        }
    }

    private static Task Post(IProgramContext context, CancellationToken token) {
        var arg = context?.Command?.Args;
        var msg = arg == null ? "" : string.Join(" ", arg);
        return CommanderDomain.Post(msg, token);
    }

    protected sealed override Task<int> Run(WindowsProgramConfig config,
                                            IProgramContext context,
                                            CancellationToken token) {
        return Lock(async owned => {
            if (owned) {
                using (await Imports(token)) {
                    var instance = new WindowsProgramInstance(config, token);
                    var instanceExitCode = await instance.Task;
                    return instanceExitCode;
                }
            }
            else {
                await Post(context, token);
                return 0;
            }
        });
    }
}
