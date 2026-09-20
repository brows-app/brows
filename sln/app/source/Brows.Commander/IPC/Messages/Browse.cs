namespace Brows.IPC.Messages;

internal sealed record Browse : CommanderMessage {
    public string ID { get; init; }
}
