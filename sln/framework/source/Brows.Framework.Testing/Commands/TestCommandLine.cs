namespace Brows.Commands;

/// <summary>
/// An <see cref="ICommandLine"/> whose parts are set by tests.
/// </summary>
public sealed class TestCommandLine : ICommandLine {
    public string Input { get; set; }
    public string Command { get; set; }
    public string Trigger { get; set; }
    public string Parameter { get; set; }
    public string Conf { get; set; }

    bool ICommandLine.HasInput(out string input) {
        input = Input;
        return input is not null && input.TrimStart(' ').Length > 0;
    }

    bool ICommandLine.HasCommand(out string command) {
        command = Command;
        return command is not null && command.Length > 0;
    }

    bool ICommandLine.HasTrigger(out string trigger) {
        trigger = Trigger;
        return trigger is not null && trigger.Length > 0;
    }

    bool ICommandLine.HasParameter(out string parameter) {
        parameter = Parameter;
        return parameter is not null && parameter.Length > 0;
    }

    bool ICommandLine.HasConf(out string conf) {
        conf = Conf;
        return string.IsNullOrWhiteSpace(conf) == false;
    }
}