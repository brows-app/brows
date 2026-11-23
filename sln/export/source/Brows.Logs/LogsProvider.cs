namespace Brows;

internal sealed class LogsProvider : Provider {
    public string Name =>
        Translation?.Value(ID);

    public ITranslation Translation { get; }

    public LogsProvider(string id, ITranslation translation) : base(id) {
        Translation = translation;
    }
}
