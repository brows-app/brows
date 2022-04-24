namespace Brows {
    using ComponentModel;

    public class Acknowledge : NotifyPropertyChanged, IAcknowledge {
        protected virtual void OnAcknowledged(AcknowledgedEventArgs e) =>
            Acknowledged?.Invoke(this, e);

        public event AcknowledgedEventHandler Acknowledged;
    }
}
