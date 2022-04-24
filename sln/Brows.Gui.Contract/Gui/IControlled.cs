namespace Brows.Gui {
    public interface IControlled<TController> {
        TController Controller { set; }
    }
}
