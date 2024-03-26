using System;

namespace Brows {
    public class ProgramContext : IProgramContext {
        public IProgramCommand Command { get; }
        public IProgramConsole Console { get; }

        public ProgramContext(IProgramCommand command, IProgramConsole console) {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Console = console;
        }

        public T Configure<T>(T target) {
            return Command.Configure(target);
        }
    }
}
