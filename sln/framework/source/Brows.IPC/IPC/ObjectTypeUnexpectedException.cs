namespace Brows.IPC;

internal sealed class ObjectTypeUnexpectedException : ObjectTypeException {
    public sealed override string Message =>
        $"Expected object of type '{ExpectedType}', but got '{ActualType}'";

    public Type ExpectedType { get; }
    public Type ActualType { get; }

    public ObjectTypeUnexpectedException(Type expectedType, Type actualType) {
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}
