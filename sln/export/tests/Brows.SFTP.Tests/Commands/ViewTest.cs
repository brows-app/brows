using Brows.Commands;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.SFTP.Native;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class ViewTest {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private SftpClientLease Lease;
    private SftpProvider Provider;
    private SftpProviderFactory Factory;

    private Func<IOperationProgress, CancellationToken, Task<bool>> CaptureOperation() {
        IPanel panel = new TestPanel { Column = 0, Provider = Provider };
        ICommandLine line = new TestCommandLine {
            Input = "view",
            Command = "Sftp_View"
        };
        Func<IOperationProgress, CancellationToken, Task<bool>> operation = null;
        var context = new Mock<ICommandContext>();
        context.Setup(item => item.HasPanel(out panel)).Returns(true);
        context.Setup(item => item.HasLine(out line)).Returns(true);
        context.Setup(item => item.Operate(It.IsAny<Func<IOperationProgress, CancellationToken, Task<bool>>>()))
            .Callback<Func<IOperationProgress, CancellationToken, Task<bool>>>(work => operation = work)
            .Returns(true);

        var command = new View();
        SetCommandCliProvider(command);
        Assert.That(((ICommand)command).TriggeredWork(context.Object), Is.True);
        Assert.That(operation, Is.Not.Null);
        return operation;
    }

    [SetUp]
    public void SetUp() {
        Factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@localhost/root/");
        Lease = Factory.Acquire(uri);
        Provider = new SftpProvider(Factory, uri, null, Lease);
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
        Lease?.Dispose();
        Lease = null;
        Factory = null;
    }

    [Test]
    public void View_TriggeredWork_TogglesPreviewWithoutPaletteParameters() {
        Assert.That(Work(parameter: null, input: "view"), Is.True);
        Assert.That(HasPreview(), Is.True);
        Assert.That(Work(parameter: null, input: "view"), Is.True);
        Assert.That(HasPreview(), Is.False);
    }

    [Test]
    public void View_TriggeredWork_TogglesPreviewWithAltPConfiguredParameter() {
        Assert.That(Work(parameter: "toggle=preview", input: "alt+p"), Is.True);
        Assert.That(HasPreview(), Is.True);
        Assert.That(Work(parameter: "toggle=preview", input: "alt+p"), Is.True);
        Assert.That(HasPreview(), Is.False);
    }

    [Test]
    public async Task View_OverlappingInvocationsDuringTeardown_ShowThenClosePreview() {
        var closeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new GatedDetail(Provider, closeGate);
        Assert.That(await Provider.Detail.ShowAsync("terminal", terminal, CancellationToken.None), Is.True);

        try {
            var firstOperation = CaptureOperation();
            var secondOperation = CaptureOperation();
            var first = firstOperation(null, CancellationToken.None);
            await terminal.CloseStarted.Task.WaitAsync(Timeout);
            var second = secondOperation(null, CancellationToken.None);
            closeGate.TrySetResult();

            Assert.That(await first.WaitAsync(Timeout), Is.True);
            Assert.That(await second.WaitAsync(Timeout), Is.True);
            Assert.That(Provider.Detail.Active, Is.Null);
        }
        finally {
            closeGate.TrySetResult();
        }
    }

    private sealed class GatedDetail : ProviderDetail, IProviderDetailLifetime {
        private readonly TaskCompletionSource CloseGate;

        protected override ProviderDetail Clone(Provider provider) {
            return new GatedDetail(provider, CloseGate);
        }

        public TaskCompletionSource CloseStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedDetail(Provider provider, TaskCompletionSource closeGate) : base(provider) {
            CloseGate = closeGate;
        }

        public void Invalidate() {
        }

        public async Task CloseAsync(CancellationToken token) {
            CloseStarted.TrySetResult();
            await CloseGate.Task.WaitAsync(token);
        }
    }

    private static void SetCommandCliProvider(object command) {
        var framework = typeof(ICommand).Assembly;
        var cliProviderType = framework.GetType("Brows.Commands.CommandCliProvider", throwOnError: true);
        var cliProvider = Activator.CreateInstance(cliProviderType, nonPublic: true);
        var baseCommandType = framework.GetType("Brows.Commands.Command", throwOnError: true);
        var property = baseCommandType.GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        property.SetValue(command, cliProvider);
    }

    private bool HasPreview() {
        return Provider.Detail.Active is not null;
    }

    private bool Work(string parameter, string input) {
        var command = new View();
        SetCommandCliProvider(command);
        var context = new TestCommandContext {
            Panels = { Panels = { new TestPanel { Column = 0, Provider = Provider } } },
            Line = new TestCommandLine {
                Input = input,
                Command = "Sftp_View",
                Trigger = input == "alt+p" ? "ALT+P" : null,
                Parameter = parameter
            }
        };
        return ((ICommand)command).TriggeredWork(context);
    }
}
