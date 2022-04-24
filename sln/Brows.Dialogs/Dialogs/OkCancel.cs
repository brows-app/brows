namespace Brows.Dialogs {
    public class OkCancel : OkBase {
        public const int Cancel = 2;

        public object CancelCommand => Request.Create(
            owner: this,
            execute: _ => Execute(Cancel),
            canExecute: _ => CanExecute(),
            dependencies: Dependencies);

        public virtual bool IsCancel() {
            return Acknowledgment == Cancel;
        }
    }
}
