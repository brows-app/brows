using Domore.Net.Sockets;
using System;

namespace Domore.IPC {
    public sealed class MessengerFactory {
        internal MessengerErrorEventArgs OnError(Exception ex) {
            var e = default(MessengerErrorEventArgs);
            var handler = ErrorHandler;
            if (handler != null) {
                handler(this, e = new MessengerErrorEventArgs(ex));
            }
            return e;
        }

        internal MessageEventArgs OnMessageSent(string message) {
            var e = default(MessageEventArgs);
            var handler = MessageHandler;
            if (handler != null) {
                handler(this, e = new MessageEventArgs(MessageEventKind.Sent, message));
            }
            return e;
        }

        internal MessageEventArgs OnMessageReceived(string message) {
            var e = default(MessageEventArgs);
            var handler = MessageHandler;
            if (handler != null) {
                handler(this, e = new MessageEventArgs(MessageEventKind.Received, message));
            }
            return e;
        }

        public event MessageEventHandler MessageHandler;
        public event MessengerErrorEventHandler ErrorHandler;

        public MessengerKind Kind { get; init; }
        public string Directory { get; init; }

        public Messenger Create() {
            var kind = Kind;
            switch (kind) {
                case MessengerKind.Default:
                    return new TcpMessenger(this);
                default:
                    throw new NotImplementedException($"{nameof(MessengerKind)} [{kind}]");
            }
        }
    }
}
