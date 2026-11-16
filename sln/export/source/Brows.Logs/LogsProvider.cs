namespace Brows;

internal sealed class LogsProvider : Provider {
    public string Name =>
        Translation.Global.Value(ID);

    public LogsProvider(string id) : base(id) {
    }
}
