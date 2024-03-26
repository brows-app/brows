using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Program : IProgram {
        protected virtual string Name =>
            GetType().Name;

        protected abstract Task<int> Run(IProgramContext context, CancellationToken token);

        string IProgram.Name =>
            Name;

        async Task<int> IProgram.Run(IProgramContext context, CancellationToken token) {
            return await Run(context, token);
        }
    }

    public abstract class Program<TConfig> : Program where TConfig : new() {
        protected abstract Task<int> Run(TConfig config, IProgramContext context, CancellationToken token);

        protected sealed override Task<int> Run(IProgramContext context, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(context);
            var config = context.Configure(new TConfig());
            return Run(config, context, token);
        }
    }
}
