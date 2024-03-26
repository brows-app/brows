using System;

namespace Brows {
    public sealed class MessageEventArgs : EventArgs {
        public IMessage Message { get; }

        public MessageEventArgs(IMessage message) {
            Message = message;
        }
    }
}
