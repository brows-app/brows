using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class WindowsProgram : Program<WindowsProgramConfig> {
        private static async Task<T> Lock<T>(Func<bool, Task<T>> func) {
            ArgumentNullException.ThrowIfNull(func);
            using (new Mutex(initiallyOwned: true, "9520EEBF-6D53-432C-9697-729DE30A31A9", out var createdNew)) {
                return await func(createdNew).ConfigureAwait(false);
            }
        }

        private static Task Post(IProgramContext context, CancellationToken token) {
            var arg = context?.Command?.Args;
            var msg = arg == null ? "" : string.Join(" ", arg);
            return CommanderDomain.Post(msg, token);
        }

        protected sealed override Task<int> Run(WindowsProgramConfig config, IProgramContext context, CancellationToken token) {
            return Lock(async createdNew => {
                if (createdNew) {
                    var instance = new WindowsProgramInstance(config, token);
                    var instanceExitCode = await instance.Task;
                    return instanceExitCode;
                }
                else {
                    await Post(context, token);
                    return 0;
                }
            });
        }

        public Task<IProgram> Program(IProgramContext context, CancellationToken token) {
            return Lock(async createdNew => {
                if (createdNew) {
                    return (IProgram)this;
                }
                else {
                    await Post(context, token).ConfigureAwait(false);
                    return null;
                }
            });
        }
    }
}
