namespace Brows {
    public class ProgramContext : IProgramContext {
        public IProgramCommand Command { get; }
        public IProgramConsole Console { get; }

        public ProgramContext(IProgramCommand command, IProgramConsole console) {
            Command = command;
            Console = console;
        }
    }
}
