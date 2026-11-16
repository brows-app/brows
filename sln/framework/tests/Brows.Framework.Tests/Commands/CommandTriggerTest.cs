using Brows.Localization;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CONF = Domore.Conf.Conf;

namespace Brows.Commands;

[TestFixture]
[NonParallelizable]
internal sealed class CommandTriggerTest {
    private ITranslation PreviousTranslation;

    private static async Task<TriggerConfig> LoadConfig(string userContent) {
        var temporaryDirectory = Directory.CreateTempSubdirectory("BrowsCommandTriggerTest-");
        var directory = Path.GetFullPath(temporaryDirectory.FullName);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var temporaryRootPrefix = Path.EndsInDirectorySeparator(temporaryRoot)
            ? temporaryRoot
            : temporaryRoot + Path.DirectorySeparatorChar;
        var directoryIsInTemp = directory.StartsWith(temporaryRootPrefix, StringComparison.OrdinalIgnoreCase);
        try {
            Assert.That(directoryIsInTemp, Is.True);
            var userFile = Path.Combine(directory, "trigger.conf");
            await File.WriteAllTextAsync(userFile, userContent);
            var config = new TriggerConfig();
            CONF.Contain(DefaultTriggerFile(), special: "brows").Configure(config, key: "");
            CONF.Contain(userFile, special: "brows").Configure(config, key: "");
            return config;
        }
        finally {
            if (directoryIsInTemp) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string DefaultTriggerFile() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            var path = Path.Combine(
                directory.FullName,
                "app",
                "source",
                "brows",
                "brows.config",
                "default",
                "trigger.conf.default");
            if (File.Exists(path)) {
                return path;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate the Brows trigger defaults from the test output directory.");
    }

    [SetUp]
    public void SetUp() {
        PreviousTranslation = Translation.Global;
        var translation = new Mock<ITranslation>();
        translation.Setup(item => item.Value(It.IsAny<string>()))
            .Returns((string key) => key switch {
                "PressModifiers_Control" => "CTRL",
                "PressModifiers_Shift" => "SHIFT",
                "PressModifiers_Alt" => "ALT",
                "PressKey_T" => "T",
                "PressKey_Y" => "Y",
                "PressKey_F12" => "F12",
                "PressKey_F11" => "F11",
                "PressKey_Back" => "BACK",
                "PressKey_Left" => "LEFT",
                _ => null
            });
        Translation.Global = translation.Object;
    }

    [TearDown]
    public void TearDown() {
        Translation.Global = PreviousTranslation;
    }

    [Test]
    public async Task Compile_LoadedDefaultsAndUserRemapExposeOnlyEffectiveReleaseGesture() {
        var config = await LoadConfig(
            "cmd[FileSystem_TerminalRelease].input = terminal-return\r\n" +
            "cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F12] = disabled\r\n" +
            "cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F11] = *\r\n");
        var release = config.Cmd["FileSystem_TerminalRelease"];
        ICommandTrigger trigger = CommandTrigger.Compile(release.Input, release.Press, release.Click, release.Macro);
        var oldGesture = new PressGesture(PressKey.F12, PressModifiers.Control | PressModifiers.Shift);
        var newGesture = new PressGesture(PressKey.F11, PressModifiers.Control | PressModifiers.Shift);

        Assert.That(trigger.Triggered("terminal-return", out _), Is.True);
        Assert.That(trigger.Triggered("terminal-release", out _), Is.False);
        Assert.That(trigger.Triggered(oldGesture, out _), Is.False);
        Assert.That(trigger.Triggered(newGesture, out var newTrigger), Is.True);
        Assert.That(newTrigger.Defined, Is.EqualTo("terminal-return"));

        var open = config.Cmd["FileSystem_TerminalOpen"];
        ICommandTrigger openTrigger = CommandTrigger.Compile(open.Input, open.Press, open.Click, open.Macro);
        var openGesture = new PressGesture(PressKey.T, PressModifiers.Control | PressModifiers.Alt);
        Assert.That(openTrigger.Triggered("terminal", out _), Is.True);
        Assert.That(openTrigger.Triggered("term", out _), Is.True);
        Assert.That(openTrigger.Triggered(openGesture, out var openGestureTrigger), Is.True);
        Assert.That(openGestureTrigger.Defined, Is.EqualTo("terminal"));
    }

    [Test]
    public async Task Compile_LoadedDefaultsWithoutUserOverlayBindReleaseAndCloseAlias() {
        var config = await LoadConfig("");
        var release = config.Cmd["FileSystem_TerminalRelease"];
        ICommandTrigger releaseTrigger = CommandTrigger.Compile(
            release.Input,
            release.Press,
            release.Click,
            release.Macro);
        var releaseGesture = new PressGesture(PressKey.F12, PressModifiers.Control | PressModifiers.Shift);

        Assert.That(releaseTrigger.Triggered("terminal-release", out _), Is.True);
        Assert.That(releaseTrigger.Triggered(releaseGesture, out var gestureTrigger), Is.True);
        Assert.That(gestureTrigger.Defined, Is.EqualTo("terminal-release"));

        var close = config.Cmd["FileSystem_TerminalClose"];
        ICommandTrigger closeTrigger = CommandTrigger.Compile(close.Input, close.Press, close.Click, close.Macro);

        Assert.That(closeTrigger.Triggered("terminal-close", out _), Is.True);
        Assert.That(closeTrigger.Gestures.Count, Is.Zero);
    }

    [Test]
    public async Task Compile_DisablesOneGestureFromGroupedDefaultWithoutRemovingSibling() {
        var config = await LoadConfig("cmd[HistoryBack].press[ALT+LEFT] = disabled\r\n");
        var historyBack = config.Cmd["HistoryBack"];
        ICommandTrigger trigger = CommandTrigger.Compile(
            historyBack.Input,
            historyBack.Press,
            historyBack.Click,
            historyBack.Macro);
        var back = new PressGesture(PressKey.Back);
        var altLeft = new PressGesture(PressKey.Left, PressModifiers.Alt);

        Assert.That(trigger.Triggered(back, out _), Is.True);
        Assert.That(trigger.Triggered(altLeft, out _), Is.False);
    }

    [Test]
    public void Compile_DisablesOnlyAWholeTrimmedMarkerValue() {
        var press = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["CTRL+ALT+T"] = "  DiSaBlEd  ",
            ["CTRL+ALT+Y"] = "mode=disabled"
        };
        ICommandTrigger trigger = CommandTrigger.Compile("terminal", press, [], []);
        var disabled = new PressGesture(PressKey.T, PressModifiers.Control | PressModifiers.Alt);
        var enabled = new PressGesture(PressKey.Y, PressModifiers.Control | PressModifiers.Alt);

        Assert.That(trigger.Triggered(disabled, out _), Is.False);
        Assert.That(trigger.Triggered(enabled, out var enabledTrigger), Is.True);
        Assert.That(enabledTrigger.Defined, Is.EqualTo("mode=disabled"));
    }

    [Test]
    public void Compile_PreservesInputAliasesAndConfiguredMacroExpansion() {
        var macro = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["terminal-start"] = "start=*"
        };
        ICommandTrigger trigger = CommandTrigger.Compile("terminal|term", [], [], macro);

        Assert.That(trigger.Triggered("terminal", out _), Is.True);
        Assert.That(trigger.Triggered("term", out _), Is.True);
        Assert.That(trigger.Triggered("terminal-start", out var macroTrigger), Is.True);
        Assert.That(macroTrigger.Defined, Is.EqualTo("start=terminal"));
    }

    private sealed class TriggerConfig {
        public Dictionary<string, TriggerCommand> Cmd { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TriggerCommand {
        public string Input { get; set; }

        public Dictionary<string, string> Press { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Click { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Macro { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
