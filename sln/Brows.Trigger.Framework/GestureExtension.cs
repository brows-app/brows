using System;

namespace Brows {
    public static class GestureExtension {
        private static readonly ClickGestureTranslation ClickGestureTranslation = new ClickGestureTranslation(Translation.Global);
        private static readonly PressGestureTranslation PressGestureTranslation = new PressGestureTranslation(Translation.Global);

        public static string Display(this IGesture gesture) {
            if (gesture is PressGesture press) {
                return PressGestureTranslation.Display(press).Display;
            }
            if (gesture is ClickGesture click) {
                return ClickGestureTranslation.Display(click).Display;
            }
            throw new InvalidGestureArgumentException(gesture);
        }

        public static IGesture Parse(this IGesture gesture, string s) {
            if (gesture is PressGesture press) {
                return PressGestureTranslation.Parse(s).Gesture;
            }
            if (gesture is ClickGesture click) {
                return ClickGestureTranslation.Parse(s).Gesture;
            }
            throw new InvalidGestureArgumentException(gesture);
        }

        public static char Delimiter(this IGesture gesture) {
            if (gesture is PressGesture press) {
                return PressGestureTranslation.Display(press).Delimiter;
            }
            if (gesture is ClickGesture click) {
                return ClickGestureTranslation.Display(click).Delimiter;
            }
            throw new InvalidGestureArgumentException(gesture);
        }

        private sealed class InvalidGestureArgumentException : ArgumentException {
            public IGesture Gesture { get; }

            public InvalidGestureArgumentException(IGesture gesture) {
                Gesture = gesture;
            }
        }
    }
}
