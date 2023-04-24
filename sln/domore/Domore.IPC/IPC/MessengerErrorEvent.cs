using System;

namespace Domore.IPC {
    public delegate void MessengerErrorEventHandler(object sender, MessengerErrorEventArgs e);

    public class MessengerErrorEventArgs : EventArgs {
        public bool Exit { get; set; }
        public Exception Exception { get; }

        public MessengerErrorEventArgs(Exception exception) {
            Exception = exception;
        }
    }
}
