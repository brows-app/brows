using System.Collections;

namespace Brows.Commands;

/// <summary>
/// Exposes an object's command source when it implements <see cref="ICommandSourceObject"/>.
/// </summary>
public sealed class CommandSource : ICommandSource {
    private ICommandSourceObject SourceObject => field ??= Object as ICommandSourceObject;

    /// <summary>
    /// Gets the object's current command item, or null when it supplies no command source.
    /// </summary>
    public object Item => field ??= SourceObject?.Instance;

    /// <summary>
    /// Gets the object's command collection, or null when it supplies no command source.
    /// </summary>
    public IEnumerable Items => field ??= SourceObject?.Collection;

    /// <summary>
    /// Gets the original source object.
    /// </summary>
    public object Object { get; }

    /// <summary>
    /// Gets the type of the original source object, or null when the object is null.
    /// </summary>
    public Type Type => field ??= Object?.GetType();

    /// <summary>
    /// Initializes a command source for an object.
    /// </summary>
    /// <param name="object">
    /// The object supplying the command source, or null when there is no source.
    /// </param>
    public CommandSource(object @object) {
        Object = @object;
    }
}
