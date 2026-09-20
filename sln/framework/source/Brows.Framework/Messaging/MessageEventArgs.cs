using System;

namespace Brows.Messaging;

public sealed class MessageEventArgs : EventArgs {
    public IMessage Message { get; }

    public MessageEventArgs(IMessage message) {
        Message = message;
    }
}
