namespace Brows.Cli {
    public class CommandLine : ICommandLine {
        public ICommandHelper Helper =>
            _Helper ?? (
            _Helper = new CommandHelper());
        private ICommandHelper _Helper;

        public ICommandParser Parser =>
            _Parser ?? (
            _Parser = new CommandParser());
        private ICommandParser _Parser;
    }
}
