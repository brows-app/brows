namespace Brows.CLI;

internal sealed class CliCommandNotFoundException : CliCommandException {
    public sealed override int ErrorCode => 1;

    public sealed override string Message =>
        $"Unknown command '{CommandName}'";

    public string CommandName { get; }

    public CliCommandNotFoundException(string commandName) {
        CommandName = commandName;
    }
}
