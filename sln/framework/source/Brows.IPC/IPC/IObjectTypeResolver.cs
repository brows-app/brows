namespace Brows.IPC;

/// <summary>
/// Implementations of this contract resolve CLR types
/// from type information.
/// </summary>
public interface IObjectTypeResolver {
    /// <summary>
    /// Gets the <see cref="Type"/> for the specified <see cref="ObjectTypeInfo"/>.
    /// </summary>
    /// <param name="info">The type information.</param>
    /// <returns>The resolved type, or null if no type could be resolved.</returns>
    public Type Resolve(ObjectTypeInfo info);

    /// <summary>
    /// Reverses the type resolution, getting info for the supplied type.
    /// </summary>
    /// <param name="type">The type for which information is returned.</param>
    /// <returns>The type information.</returns>
    public ObjectTypeInfo Reverse(Type type);
}
