using System;

namespace Brows.Triggers {
    internal sealed class GestureTrigger : IGestureTrigger {
        public string Defined { get; }
        public string Display => Gesture.Display();
        public IGesture Gesture { get; }

        public GestureTrigger(string defined, IGesture gesture) {
            Defined = defined;
            Gesture = gesture ?? throw new ArgumentNullException(nameof(gesture));
        }

        bool IGestureTrigger.Triggered(IGesture gesture) {
            return Gesture.Equals(gesture);
        }
    }
}
