using Brows.Agent;
using Brows.Programs;
using Domore.Logs;
using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public sealed class WindowsProgram : Program<WindowsProgramConfig> {
    private static readonly ILog Log = Logging.For(typeof(WindowsProgram));
    private static readonly string LockName = LockNameFactory();

    private static string LockNameFactory() {
#if DEBUG
        /*
         * An unnamed semaphore lets each debug process run its own application instance.
         */
        return null;
#else
        using (var identity = WindowsIdentity.GetCurrent()) {
            var sid = identity.User?.Value ?? identity.Name.Replace('\\', '.');
            /*
             * Global namespace so that the same user gets one instance across all sessions
             * (console, RDP, fast user switching), qualified by SID so that each user gets
             * their own.
             */
            return $"Global\\Brows.WindowsProgram.{sid}";
        }
#endif
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
#if !DEBUG
        /*
         * Refuse an agent session before the single-instance check, so the launch is neither shown nor
         * forwarded to a running instance.
         */
        if (AgentBridge.Requested(config?.Agent?.Pipe)) {
            if (Log.Warn()) {
                Log.Warn("agent.pipe is refused: agent sessions require a Debug build");
            }
            return Task.FromResult(1);
        }
#endif
        return Lock(async owned => {
            if (owned) {
                using var agentBridge = AgentBridge.Create(config.Agent?.Pipe);
                using (await Imports(token)) {
                    var instance = new WindowsProgramInstance(config, agentBridge, token);
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
