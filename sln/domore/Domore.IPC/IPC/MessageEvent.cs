using System;

namespace Domore.IPC {
    public delegate void MessageEventHandler(object sender, MessageEventArgs e);

    public sealed class MessageEventArgs : EventArgs {
        public MessageEventKind Kind { get; }
        public string Message { get; }

        public MessageEventArgs(MessageEventKind kind, string message) {
            Kind = kind;
            Message = message;
        }
    }
}
