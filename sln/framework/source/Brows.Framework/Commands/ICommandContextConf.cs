namespace Brows.Commands;

public interface ICommandContextConf {
    ICommand Command { get; }
    string Text { get; }
}
