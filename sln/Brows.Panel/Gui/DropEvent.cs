using System;

namespace Brows.Gui {
    public delegate void DropEventHandler(object sender, DropEventArgs e);

    public class DropEventArgs : EventArgs {
        public IPayload Payload { get; }

        public DropEventArgs(IPayload payload) {
            Payload = payload;
        }
    }
}
