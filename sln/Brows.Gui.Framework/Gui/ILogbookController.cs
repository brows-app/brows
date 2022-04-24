namespace Brows.Gui {
    public interface ILogbookController : IController {
        double PageDown();
        double PageUp();
        double ScrollDown();
        double ScrollUp();
        double ScrollTo(double offset);
    }
}
