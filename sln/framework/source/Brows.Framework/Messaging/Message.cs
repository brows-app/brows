namespace Brows.Messaging;

public abstract class Message : IMessage {
    public abstract MessageType Type { get; }
}
