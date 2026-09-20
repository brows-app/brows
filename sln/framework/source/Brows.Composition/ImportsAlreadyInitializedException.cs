namespace Brows;

public sealed class ImportsAlreadyInitializedException : Exception {
    public sealed override string Message => "The imports have already been initialized.";
}
