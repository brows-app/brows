using Brows.IPC.Messages;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows.IPC;

internal sealed class CommanderMessageDelegate : ObjectDelegate {
    public sealed override IObjectTypeResolver TypeResolver =>
        ObjectTypeResolver.Lookup(Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.Namespace.StartsWith("Brows.Messages.")));

    public sealed override Task<object> Call(object input, CancellationToken token) {
        if (input is Browse browse) {

        }
        return Task.FromResult<object>(default);
    }

    protected override Task<bool> Subscribe(CancellationToken token) {
        if (token.IsCancellationRequested) {
            return Task.FromCanceled<bool>(token);
        }
        return Task.FromResult(false);
    }

    private sealed class Factory : Factory<CommanderMessageDelegate> {
    }
}
