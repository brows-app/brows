namespace Brows.Gui {
    using Triggers;

    public interface ITriggerController : IController {
        event InputEventHandler WindowInput;
        event KeyboardKeyEventHandler WindowKeyboardKeyDown;
    }
}
