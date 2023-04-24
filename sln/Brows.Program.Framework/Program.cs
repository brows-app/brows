using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Program : IProgram {
        protected virtual string Name =>
            GetType().Name;

        protected abstract Task<int> Run(IProgramContext context, CancellationToken cancellationToken);

        string IProgram.Name =>
            Name;

        async Task<int> IProgram.Run(IProgramContext context, CancellationToken token) {
            return await Run(context, token);
        }
    }
}
