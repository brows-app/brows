using System;

namespace Brows {
    public sealed class GestureEventArgs : EventArgs {
        public bool Triggered { get; set; }
        public IGesture Gesture { get; }

        public GestureEventArgs(IGesture gesture) {
            Gesture = gesture;
        }
    }
}
