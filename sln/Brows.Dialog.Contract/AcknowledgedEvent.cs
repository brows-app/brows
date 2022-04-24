using System;

namespace Brows {
    public delegate void AcknowledgedEventHandler(object sender, AcknowledgedEventArgs e);

    public class AcknowledgedEventArgs : EventArgs {
        public object Acknowledgment { get; }

        public AcknowledgedEventArgs(object acknowledgment) {
            Acknowledgment = acknowledgment;
        }
    }
}
