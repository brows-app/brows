using Domore.Notification;

namespace Brows {
    public class Acknowledge : Notifier, IAcknowledge {
        protected virtual void OnAcknowledged(AcknowledgedEventArgs e) =>
            Acknowledged?.Invoke(this, e);

        public event AcknowledgedEventHandler Acknowledged;
    }
}
