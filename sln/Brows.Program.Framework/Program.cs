using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Program : IProgram {
        protected virtual string Name =>
            GetType().Name;

        protected abstract Task<int> Run(IProgramContext context, CancellationToken cancellationToken);

        string IProgram.Name =>
            Name;

        async Task<int> IProgram.Run(IProgramCommand command, CancellationToken cancellationToken) {
            if (null == command) throw new ArgumentNullException(nameof(command));
            var context = new ProgramContext(command);
            return await Run(context, cancellationToken);
        }
    }
}
