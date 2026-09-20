using System.Collections.Generic;

namespace Brows.Commands;

[TestFixture]
internal sealed class CommandTest {
    [Test]
    public void Command_TriggeredWork_WhenDisabled_ReturnsFalse() {
        var command = new TestCommand(enabled: false);

        var worked = ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenEnabled_InvokesWork() {
        var command = new TestCommand(enabled: true);

        ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(command.Triggered, Is.True);
    }

    [Test]
    public void CommandOfT_TriggeredWork_WhenLineHasParameter_PassesParsedParameterToWork() {
        var command = new ParameterCommand();
        var context = new Mock<ICommandContext>();
        var line = new Mock<ICommandLine>();
        ICommandLine commandLine = line.Object;
        var parameter = "value";
        string conf = null;
        context.Setup(value => value.HasLine(out commandLine)).Returns(true);
        line.Setup(value => value.HasParameter(out parameter)).Returns(true);
        line.Setup(value => value.HasConf(out conf)).Returns(false);

        ((ICommand)command).TriggeredWork(context.Object);

        Assert.That(command.ParameterValue.Value, Is.EqualTo(parameter));
    }

    [Test]
    public void CommandOfT_TriggeredWork_WhenLineHasNoParameter_PassesNewParameterToWork() {
        var command = new ParameterCommand();

        ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(command.ParameterValue.Value, Is.Null);
    }

    [Test]
    public void Command_Enabled_WhenNotConfigured_IsTrue() {
        var command = new DefaultCommand();

        Assert.That(((ICommand)command).Enabled, Is.True);
    }

    [Test]
    public void Command_TriggeredWork_WhenNotOverridden_ReturnsFalse() {
        var command = new DefaultCommand();

        var worked = ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenContextIsNull_InvokesWork() {
        var command = new TestCommand(enabled: true);

        ((ICommand)command).TriggeredWork(null);

        Assert.That(command.Triggered, Is.True);
    }

    [Test]
    public void Command_ArbitraryWork_WhenDisabled_ReturnsFalse() {
        var command = new ArbitraryTestCommand(enabled: false);

        var worked = ((ICommand)command).ArbitraryWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_ArbitraryWork_WhenEnabled_InvokesWork() {
        var command = new ArbitraryTestCommand(enabled: true);

        ((ICommand)command).ArbitraryWork(Mock.Of<ICommandContext>());

        Assert.That(command.Arbitrary, Is.True);
    }

    [Test]
    public void Command_ArbitraryWork_WhenNotOverridden_ReturnsFalse() {
        var command = new DefaultCommand();

        var worked = ((ICommand)command).ArbitraryWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenContextHasNoSource_ReturnsFalse() {
        var command = new SourcedCommand();

        var worked = ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenContextIsNullAndSourceRequired_ReturnsFalse() {
        var command = new SourcedCommand();

        var worked = ((ICommand)command).TriggeredWork(null);

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenSourceTypeDoesNotMatch_ReturnsFalse() {
        var command = new SourcedCommand();

        var worked = ((ICommand)command).TriggeredWork(SourceContext(42));

        Assert.That(worked, Is.False);
    }

    [Test]
    public void Command_TriggeredWork_WhenSourceTypeMatches_InvokesWork() {
        var command = new SourcedCommand();

        ((ICommand)command).TriggeredWork(SourceContext("item"));

        Assert.That(command.Triggered, Is.True);
    }

    [Test]
    public void CommandOfT_TriggeredWork_WhenContextIsNull_ReturnsFalse() {
        var command = new ParameterCommand();

        var worked = ((ICommand)command).TriggeredWork(null);

        Assert.That(worked, Is.False);
    }

    [Test]
    public void CommandOfT_TriggeredWork_WhenWorkIsNotOverridden_ReturnsFalse() {
        var command = new DefaultParameterCommand();

        var worked = ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(worked, Is.False);
    }

    [Test]
    public void CommandOfT_HasParameter_WhenLineHasNoParameterOrConf_ReturnsFalse() {
        var command = new ParameterCommand();

        ((ICommand)command).TriggeredWork(Mock.Of<ICommandContext>());

        Assert.That(command.ParameterExists, Is.False);
    }

    [Test]
    public void CommandOfT_TriggeredWork_WhenLineHasOnlyConf_PassesConfToFactory() {
        var command = new ParameterCommand();

        ((ICommand)command).TriggeredWork(LineContext(parameter: null, conf: "conf"));

        Assert.That(command.ParameterValue.Conf, Is.EqualTo("conf"));
    }

    [Test]
    public void CommandOfT_HasParameter_WhenCalledTwice_InvokesFactoryOnce() {
        var command = new ParameterCommand(callCount: 2);

        ((ICommand)command).TriggeredWork(LineContext(parameter: "value", conf: null));

        Assert.That(command.FactoryCalls, Is.EqualTo(1));
    }

    [Test]
    public void CommandOfT_GetParameter_WhenFactoryThrows_ReturnsFalse() {
        var command = new ThrowingParameterCommand();

        ((ICommand)command).TriggeredWork(LineContext(parameter: "value", conf: null));

        Assert.That(command.ParameterObtained, Is.False);
    }

    [Test]
    public void CommandOfT_HasParameterError_WhenFactoryThrows_ProvidesError() {
        var command = new ThrowingParameterCommand();

        ((ICommand)command).TriggeredWork(LineContext(parameter: "value", conf: null));

        Assert.That(command.ParameterError, Is.TypeOf<FormatException>());
    }

    [Test]
    public void CommandOfT_HasParameterError_WhenParameterIsValid_ReturnsFalse() {
        var command = new ParameterCommand();

        ((ICommand)command).TriggeredWork(LineContext(parameter: "value", conf: null));

        Assert.That(command.ParameterError, Is.Null);
    }

    private static ICommandContext LineContext(string parameter, string conf) {
        var context = new Mock<ICommandContext>();
        var line = new Mock<ICommandLine>();
        ICommandLine commandLine = line.Object;
        context.Setup(value => value.HasLine(out commandLine)).Returns(true);
        line.Setup(value => value.HasParameter(out parameter)).Returns(parameter != null);
        line.Setup(value => value.HasConf(out conf)).Returns(conf != null);
        return context.Object;
    }

    private static ICommandContext SourceContext<T>(T item) {
        var context = new Mock<ICommandContext>();
        object source = item;
        IReadOnlyList<object> sources = [source];
        context.Setup(value => value.HasSource(out source, out sources)).Returns(true);
        return context.Object;
    }

    private sealed class TestCommand(bool enabled) : Command {
        public bool Triggered { get; private set; }

        protected override bool Enabled => enabled;

        protected override bool TriggeredWork(ICommandContext context) {
            Triggered = true;
            return true;
        }
    }

    private sealed class ArbitraryTestCommand(bool enabled) : Command {
        public bool Arbitrary { get; private set; }

        protected override bool Enabled => enabled;

        protected override bool ArbitraryWork(ICommandContext context) {
            Arbitrary = true;
            return true;
        }
    }

    private sealed class DefaultCommand : Command {
    }

    private sealed class SourcedCommand : Command {
        public bool Triggered { get; private set; }

        protected override IEnumerable<Type> Source => [typeof(string)];

        protected override bool TriggeredWork(ICommandContext context) {
            Triggered = true;
            return true;
        }
    }

    private sealed class DefaultParameterCommand : Command<ParameterCommand.Parameter> {
    }

    private sealed class ThrowingParameterCommand : Command<ParameterCommand.Parameter> {
        public bool ParameterObtained { get; private set; } = true;
        public Exception ParameterError { get; private set; }

        protected override ParameterCommand.Parameter ParameterFactory(string parameter, string conf) {
            throw new FormatException("Invalid parameter.");
        }

        protected override bool Work(Context context) {
            ParameterObtained = context.GetParameter(out _);
            context.HasParameterError(out var error);
            ParameterError = error;
            return true;
        }
    }

    private sealed class ParameterCommand(int callCount) : Command<ParameterCommand.Parameter> {
        public int FactoryCalls { get; private set; }
        public bool ParameterExists { get; private set; }
        public Parameter ParameterValue { get; private set; }
        public Exception ParameterError { get; private set; }

        public ParameterCommand() : this(callCount: 1) {
        }

        protected override Parameter ParameterFactory(string parameter, string conf) {
            FactoryCalls++;
            return new Parameter { Value = parameter, Conf = conf };
        }

        protected override bool Work(Context context) {
            for (var i = 0; i < callCount; i++) {
                ParameterExists = context.HasParameter(out _);
            }
            context.GetParameter(out var parameter);
            context.HasParameterError(out var error);
            ParameterValue = parameter;
            ParameterError = error;
            return true;
        }

        public sealed class Parameter {
            public string Value { get; init; }
            public string Conf { get; init; }
        }
    }
}
