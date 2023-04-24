using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class WindowsProgram : Program {
        private async Task<T> Lock<T>(Func<bool, Task<T>> func) {
            if (func is null) throw new ArgumentNullException(nameof(func));
            using (new Mutex(initiallyOwned: true, "9520EEBF-6D53-432C-9697-729DE30A31A9", out var createdNew)) {
                return await func(createdNew);
            }
        }

        private async Task Post(IProgramContext context, CancellationToken token) {
            var arg = context?.Command?.Args;
            var msg = arg == null ? "" : string.Join(" ", arg);
            await CommanderDomain.Post(msg, token);
        }

        protected sealed override async Task<int> Run(IProgramContext context, CancellationToken token) {
            return await Lock(async createdNew => {
                if (createdNew) {
                    var instance = new WindowsProgramInstance(context, token);
                    var instanceExitCode = await instance.Task;
                    return instanceExitCode;
                }
                else {
                    await Post(context, token);
                    return 0;
                }
            });
        }

        public async Task<IProgram> Program(IProgramContext context, CancellationToken token) {
            return await Lock(async createdNew => {
                if (createdNew) {
                    return this;
                }
                else {
                    await Post(context, token);
                    return null;
                }
            });
        }
    }
}
