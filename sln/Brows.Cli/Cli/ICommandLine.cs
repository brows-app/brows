namespace Brows.Cli {
    public interface ICommandLine {
        ICommandHelper Helper { get; }
        ICommandParser Parser { get; }
    }
}
