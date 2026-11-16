using Brows.Conf;
using Brows.Gui;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class ThemeTest {
    [TestCase(
        "DarkGreen",
        CommanderThemeMode.Dark,
        TestName = "Init_InvalidName_FallsBackToFileExplorerDark")]
    [TestCase(
        "FileExplorer",
        (CommanderThemeMode)5,
        TestName = "Init_InvalidMode_FallsBackToFileExplorerDark")]
    public async Task Init_InvalidConfiguredThemeFallsBackToFileExplorerDarkAndCompletes(
        string name,
        CommanderThemeMode mode) {
        var configuredTheme = new CommanderTheme {
            Name = name,
            Mode = mode
        };
        var config = new Mock<IConfiguration<CommanderTheme>>();
        config.Setup(value => value.Load(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuredTheme);
        var configData = new Mock<IConfigurationData>();
        configData.Setup(value => value.Data<CommanderTheme>("theme"))
            .Returns(config.Object);
        var themeService = new Mock<ICommanderTheme>();
        themeService.Setup(value => value.Apply(It.Is<CommanderTheme>(theme =>
                theme.Name == name && theme.Mode == mode)))
            .Throws(new ArgumentException("Invalid configured theme."));
        var command = new Theme {
            ConfigData = configData.Object,
            CommanderTheme = themeService.Object
        };

        await ((ICommand)command).Init(CancellationToken.None);

        themeService.Verify(value => value.Apply(It.Is<CommanderTheme>(theme =>
            theme.Name == "FileExplorer" && theme.Mode == CommanderThemeMode.Dark)), Times.Once);
    }

    [Test]
    public async Task Init_ValidConfiguredThemeAppliesConfiguredTheme() {
        var configuredTheme = new CommanderTheme {
            Name = "FileExplorer",
            Mode = CommanderThemeMode.Light
        };
        var config = new Mock<IConfiguration<CommanderTheme>>();
        config.Setup(value => value.Load(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuredTheme);
        var configData = new Mock<IConfigurationData>();
        configData.Setup(value => value.Data<CommanderTheme>("theme"))
            .Returns(config.Object);
        var themeService = new Mock<ICommanderTheme>();
        var command = new Theme {
            ConfigData = configData.Object,
            CommanderTheme = themeService.Object
        };

        await ((ICommand)command).Init(CancellationToken.None);

        themeService.Verify(value => value.Apply(It.Is<CommanderTheme>(theme =>
            theme.Name == "FileExplorer" && theme.Mode == CommanderThemeMode.Light)), Times.Once);
    }

    [Test]
    public async Task Init_NonValidationFailurePropagates() {
        var configuredTheme = new CommanderTheme();
        var config = new Mock<IConfiguration<CommanderTheme>>();
        config.Setup(value => value.Load(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuredTheme);
        var configData = new Mock<IConfigurationData>();
        configData.Setup(value => value.Data<CommanderTheme>("theme"))
            .Returns(config.Object);
        var themeService = new Mock<ICommanderTheme>();
        themeService.Setup(value => value.Apply(It.IsAny<CommanderTheme>()))
            .Throws(new InvalidOperationException("Service failure."));
        var command = new Theme {
            ConfigData = configData.Object,
            CommanderTheme = themeService.Object
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ((ICommand)command).Init(CancellationToken.None));
        Assert.That(exception.Message, Is.EqualTo("Service failure."));
    }
}
