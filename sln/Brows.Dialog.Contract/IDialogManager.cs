namespace Brows {
    public interface IDialogManager {
        IDialog Current { get; }
        IDialog Show(object data);
        IDialog Hide();
    }
}
