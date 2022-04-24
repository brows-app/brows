namespace Brows.Dialogs {
    public class OkCancelAll : OkCancel {
        public const int CancelAll = 3;

        public object CancelAllCommand => Request.Create(
            owner: this,
            execute: _ => Execute(CancelAll),
            canExecute: _ => CanExecute(),
            dependencies: Dependencies);

        public override bool IsCancel() {
            return base.IsCancel() || IsCancelAll();
        }

        public virtual bool IsCancelAll() {
            return Acknowledgment == CancelAll;
        }
    }
}
