namespace Brows {
    public class CommandContextDataGesture {
        protected CommandContextDataGesture() {
        }

        public static PressGesture Up { get; } = new PressGesture(PressKey.Up, PressModifiers.Alt);
        public static PressGesture Down { get; } = new PressGesture(PressKey.Down, PressModifiers.Alt);
        public static PressGesture PageUp { get; } = new PressGesture(PressKey.PageUp, PressModifiers.Alt);
        public static PressGesture PageDown { get; } = new PressGesture(PressKey.PageDown, PressModifiers.Alt);
        public static PressGesture Previous { get; } = new PressGesture(PressKey.Left, PressModifiers.Alt);
        public static PressGesture Next { get; } = new PressGesture(PressKey.Right, PressModifiers.Alt);
        public static PressGesture Enter { get; } = new PressGesture(PressKey.Enter, PressModifiers.Alt);
        public static PressGesture Remove { get; } = new PressGesture(PressKey.D3, PressModifiers.Shift | PressModifiers.Alt);
        public static PressGesture Clear { get; } = new PressGesture(PressKey.D3, PressModifiers.Shift | PressModifiers.Alt | PressModifiers.Control);
    }
}
