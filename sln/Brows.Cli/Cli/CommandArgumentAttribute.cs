namespace Brows.Cli {
    public class CommandArgumentAttribute : CommandTokenAttribute {
        public int Order { get; set; } = -1;
    }
}
