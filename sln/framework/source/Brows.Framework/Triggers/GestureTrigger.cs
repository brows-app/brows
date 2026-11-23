using Brows.Localization;

namespace Brows.Triggers;

internal sealed class GestureTrigger : IGestureTrigger {
    public string Defined { get; }
    public string Display => Gesture.Display(Translation);
    public IGesture Gesture { get; }
    public ITranslation Translation { get; }

    public GestureTrigger(string defined, IGesture gesture, ITranslation translation) {
        Defined = defined;
        Gesture = gesture ?? throw new ArgumentNullException(nameof(gesture));
        Translation = translation ?? throw new ArgumentNullException(nameof(translation));
    }

    bool IGestureTrigger.Triggered(IGesture gesture) {
        return Gesture.Equals(gesture);
    }
}
