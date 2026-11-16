using System.Collections;

namespace Brows.Commands;

public sealed class CommandSource : ICommandSource {
    private ICommandSourceObject SourceObject => field ??= Object as ICommandSourceObject;

    public object Item => field ??= SourceObject?.Instance;

    public IEnumerable Items => field ??= SourceObject?.Collection;

    public object Object { get; }

    public Type Type => field ??= Object?.GetType();

    public CommandSource(object @object) {
        Object = @object;
    }
}
