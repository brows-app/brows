namespace Brows.Commands;

[TestFixture]
internal sealed class TestCommandLineTest {
    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("  ", false)]
    [TestCase(" x ", true)]
    [TestCase(" x", true)]
    public void HasInput_Value_ReturnsWhetherInputHasContent(string input, bool expected) {
        var line = new TestCommandLine { Input = input };
        Assert.That(((ICommandLine)line).HasInput(out var value), Is.EqualTo(expected));
        Assert.That(value, Is.EqualTo(input));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("cmd", true)]
    public void HasCommand_Value_ReturnsWhetherCommandIsSet(string command, bool expected) {
        var line = new TestCommandLine { Command = command };
        Assert.That(((ICommandLine)line).HasCommand(out var value), Is.EqualTo(expected));
        Assert.That(value, Is.EqualTo(command));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("trigger", true)]
    public void HasTrigger_Value_ReturnsWhetherTriggerIsSet(string trigger, bool expected) {
        var line = new TestCommandLine { Trigger = trigger };
        Assert.That(((ICommandLine)line).HasTrigger(out var value), Is.EqualTo(expected));
        Assert.That(value, Is.EqualTo(trigger));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("auto", true)]
    public void HasParameter_Value_ReturnsWhetherParameterIsSet(string parameter, bool expected) {
        var line = new TestCommandLine { Parameter = parameter };
        Assert.That(((ICommandLine)line).HasParameter(out var value), Is.EqualTo(expected));
        Assert.That(value, Is.EqualTo(parameter));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase(" ", false)]
    [TestCase("conf", true)]
    public void HasConf_Value_ReturnsWhetherConfIsSet(string conf, bool expected) {
        var line = new TestCommandLine { Conf = conf };
        Assert.That(((ICommandLine)line).HasConf(out var value), Is.EqualTo(expected));
        Assert.That(value, Is.EqualTo(conf));
    }
}