using System;

namespace Brows {
    public sealed class GestureEventArgs : EventArgs {
        public bool Triggered { get; set; }
        public object Source { get; }
        public IGesture Gesture { get; }

        public GestureEventArgs(IGesture gesture, object source) {
            Source = source;
            Gesture = gesture;
        }
    }
}
