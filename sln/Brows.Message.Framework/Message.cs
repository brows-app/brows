namespace Brows {
    public abstract class Message : IMessage {
        public abstract MessageType Type { get; }
    }
}
