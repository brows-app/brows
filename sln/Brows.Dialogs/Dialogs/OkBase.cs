namespace Brows.Dialogs {
    public class OkBase : Acknowledge {
        protected string[] Dependencies =>
            new[] { nameof(Acknowledgment) };

        protected void Execute(int acknowledgment) {
            Acknowledgment = acknowledgment;
            OnAcknowledged(new AcknowledgedEventArgs(Acknowledgment));
        }

        protected bool CanExecute() {
            return Acknowledgment == 0;
        }

        public const int Ok = 1;

        public int Acknowledgment {
            get => _Acknowledgment;
            private set => Change(ref _Acknowledgment, value, nameof(Acknowledgment));
        }
        private int _Acknowledgment;

        public object OkCommand => Request.Create(
            owner: this,
            execute: _ => Execute(Ok),
            canExecute: _ => CanExecute(),
            dependencies: Dependencies);

        public virtual bool IsOk() {
            return Acknowledgment == Ok;
        }
    }
}
