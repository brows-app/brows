namespace Brows.Dialogs {
    public class OkAllCancelAll : OkCancelAll {
        public const int OkAll = 4;

        public object OkAllCommand => Request.Create(
            owner: this,
            execute: _ => Execute(OkAll),
            canExecute: _ => CanExecute(),
            dependencies: Dependencies);

        public override bool IsOk() {
            return base.IsOk() || IsOkAll();
        }

        public virtual bool IsOkAll() {
            return Acknowledgment == OkAll;
        }
    }
}
