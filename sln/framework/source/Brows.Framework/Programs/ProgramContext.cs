namespace Brows.Programs;

public class ProgramContext : IProgramContext {
    public IProgramCommand Command { get; }

    public ProgramContext(IProgramCommand command) {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public T Configure<T>(T target) {
        return Command.Configure(target);
    }
}
