using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class WindowsProgram : Program, IProgramExport {
        protected override async Task<int> Run(IProgramContext context, CancellationToken cancellationToken) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            using (new Mutex(initiallyOwned: true, "9520EEBF-6D53-432C-9697-729DE30A31A9", out var createdNew)) {
                if (createdNew) {
                    var instance = new WindowsProgramInstance(context, cancellationToken);
                    var instanceExitCode = await instance.Task;
                    return instanceExitCode;
                }
                else {
                    var arg = context.Command?.Args;
                    var msg = arg == null ? "" : string.Join(" ", arg);
                    await CommanderService.Post(msg, cancellationToken);
                    return 0;
                }
            }
        }
    }
}
