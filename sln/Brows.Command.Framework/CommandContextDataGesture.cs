namespace Brows {
    using Triggers;

    public class CommandContextDataGesture {
        protected CommandContextDataGesture() {
        }

        public static KeyboardGesture Up { get; } = new KeyboardGesture(KeyboardKey.Up, KeyboardModifiers.Alt);
        public static KeyboardGesture Down { get; } = new KeyboardGesture(KeyboardKey.Down, KeyboardModifiers.Alt);
        public static KeyboardGesture PageUp { get; } = new KeyboardGesture(KeyboardKey.PageUp, KeyboardModifiers.Alt);
        public static KeyboardGesture PageDown { get; } = new KeyboardGesture(KeyboardKey.PageDown, KeyboardModifiers.Alt);
        public static KeyboardGesture Previous { get; } = new KeyboardGesture(KeyboardKey.Left, KeyboardModifiers.Alt);
        public static KeyboardGesture Next { get; } = new KeyboardGesture(KeyboardKey.Right, KeyboardModifiers.Alt);
        public static KeyboardGesture Enter { get; } = new KeyboardGesture(KeyboardKey.Enter, KeyboardModifiers.Alt);
        public static KeyboardGesture Remove { get; } = new KeyboardGesture(KeyboardKey.D3, KeyboardModifiers.Shift | KeyboardModifiers.Alt);
        public static KeyboardGesture Clear { get; } = new KeyboardGesture(KeyboardKey.D3, KeyboardModifiers.Shift | KeyboardModifiers.Alt | KeyboardModifiers.Control);
    }
}
