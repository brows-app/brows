namespace Brows {
    public class CommanderTheme {
        public string Base { get; }
        public string Background { get; }
        public string Foreground { get; }

        public CommanderTheme(string @base, string background, string foreground) {
            Base = @base;
            Background = background;
            Foreground = foreground;
        }
    }
}
