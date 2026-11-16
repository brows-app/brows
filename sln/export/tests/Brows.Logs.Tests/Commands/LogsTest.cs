using Brows.Panels;
using System;
using System.Reflection;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
internal sealed class LogsTest {
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

    private static bool Work(TestCommandContext context) {
        var command = new Logs();
        SetCommandCliProvider(command);
        return ((ICommand)command).TriggeredWork(context);
    }

    private static TestCommandContext Context(ICommander commander, IPanel panel, string parameter) {
        var context = new TestCommandContext {
            Commander = commander,
            Line = new TestCommandLine {
                Input = parameter is null ? "logs" : $"logs {parameter}",
                Command = "Logs",
                Parameter = parameter
            }
        };
        if (panel is not null) {
            context.Panels.Panels.Add(panel);
        }
        return context;
    }

    [Test]
    public void TriggeredWork_WithoutParameter_AddsPanelThatShowsLogs() {
        var commander = new Mock<ICommander>();
        commander.Setup(item => item.AddPanel("Logs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var panel = new Mock<IPanel>();
        var context = Context(commander.Object, panel.Object, parameter: null);

        Assert.That(Work(context), Is.True);
        commander.Verify(item => item.AddPanel("Logs", It.IsAny<CancellationToken>()), Times.Once);
        panel.Verify(item => item.Provide(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void TriggeredWork_WithHere_ShowsLogsInActivePanel() {
        var commander = new Mock<ICommander>(MockBehavior.Strict);
        var panel = new Mock<IPanel>();
        panel.Setup(item => item.Provide("Logs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = Context(commander.Object, panel.Object, parameter: "h");

        Assert.That(Work(context), Is.True);
        panel.Verify(item => item.Provide("Logs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void TriggeredWork_WhenPanelIsNotAdded_ReturnsFalse() {
        var commander = new Mock<ICommander>();
        commander.Setup(item => item.AddPanel("Logs", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var context = Context(commander.Object, panel: null, parameter: null);

        Assert.That(Work(context), Is.False);
    }
}
