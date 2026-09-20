namespace Brows.IPC;

internal sealed class ObjectTypeNotFoundException : ObjectTypeException {
    public sealed override string Message =>
        $"Type info for '{Type}' was not found.";

    public Type Type { get; }

    public ObjectTypeNotFoundException(Type type) {
        Type = type;
    }
}
