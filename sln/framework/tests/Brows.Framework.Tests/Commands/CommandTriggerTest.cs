using Brows.Localization;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CONF = Domore.Conf.Conf;

namespace Brows.Commands;

[TestFixture]
[NonParallelizable]
internal sealed class CommandTriggerTest {
    private ITranslation Translation;

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
                "PressKey_Delete" => "DEL",
                "ClickButton_Left" => "L",
                _ => null
            });
        Translation = translation.Object;
    }

    [TearDown]
    public void TearDown() {
        Translation = null;
    }

    [Test]
    public async Task Compile_LoadedDefaultsAndUserRemapExposeOnlyEffectiveTerminalGestures() {
        var config = await LoadConfig(
            "cmd[FileSystem_Terminal].press[CTRL+SHIFT+F12] = disabled\r\n" +
            "cmd[FileSystem_Terminal].press[CTRL+SHIFT+F11] = *\r\n");
        var terminal = config.Cmd["FileSystem_Terminal"];
        ICommandTrigger trigger = CommandTrigger.Compile(
            terminal.Input,
            terminal.Press,
            terminal.Click,
            terminal.Macro,
            Translation);
        var oldGesture = new PressGesture(PressKey.F12, PressModifiers.Control | PressModifiers.Shift);
        var newGesture = new PressGesture(PressKey.F11, PressModifiers.Control | PressModifiers.Shift);
        var closeGesture = new PressGesture(
            PressKey.F12,
            PressModifiers.Control | PressModifiers.Shift | PressModifiers.Alt);

        Assert.That(trigger.Triggered("terminal", out _), Is.True);
        Assert.That(trigger.Triggered(oldGesture, out _), Is.False);
        Assert.That(trigger.Triggered(newGesture, out var newTrigger), Is.True);
        Assert.That(newTrigger.Defined, Is.EqualTo("terminal"));
        Assert.That(trigger.Triggered(closeGesture, out var closeTrigger), Is.True);
        Assert.That(closeTrigger.Defined, Is.EqualTo("terminal close"));
        Assert.That(trigger.Gestures, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Compile_LoadedDefaultsWithoutUserOverlayBindTerminal() {
        var config = await LoadConfig("");
        var terminal = config.Cmd["FileSystem_Terminal"];
        ICommandTrigger trigger = CommandTrigger.Compile(
            terminal.Input,
            terminal.Press,
            terminal.Click,
            terminal.Macro,
            Translation);
        var gesture = new PressGesture(PressKey.F12, PressModifiers.Control | PressModifiers.Shift);
        var closeGesture = new PressGesture(
            PressKey.F12,
            PressModifiers.Control | PressModifiers.Shift | PressModifiers.Alt);
        var formerOpenGesture = new PressGesture(PressKey.T, PressModifiers.Control | PressModifiers.Alt);

        Assert.That(trigger.Triggered("terminal", out _), Is.True);
        Assert.That(trigger.Triggered("term", out _), Is.True);
        Assert.That(trigger.Triggered(gesture, out var gestureTrigger), Is.True);
        Assert.That(gestureTrigger.Defined, Is.EqualTo("terminal"));
        Assert.That(trigger.Triggered(closeGesture, out var closeTrigger), Is.True);
        Assert.That(closeTrigger.Defined, Is.EqualTo("terminal close"));
        Assert.That(trigger.Triggered(formerOpenGesture, out _), Is.False);
        Assert.That(trigger.Triggered("terminal-close", out _), Is.False);
        Assert.That(trigger.Gestures, Has.Count.EqualTo(2));
        Assert.That(config.Cmd.ContainsKey("FileSystem_TerminalOpen"), Is.False);
        Assert.That(config.Cmd.ContainsKey("FileSystem_TerminalRelease"), Is.False);
        Assert.That(config.Cmd.ContainsKey("FileSystem_TerminalClose"), Is.False);
    }

    private static Func<string, CommandTrigger.Layer> Find(TriggerConfig config) {
        return key => config.Cmd.TryGetValue(key, out var cmd)
            ? new CommandTrigger.Layer(cmd.Input, cmd.Press, cmd.Click, cmd.Macro)
            : null;
    }

    private static Dictionary<string, string> Set(params (string Key, string Value)[] items) {
        var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items) {
            set[item.Key] = item.Value;
        }
        return set;
    }

    private static Func<string, CommandTrigger.Layer> Find(params (string Key, CommandTrigger.Layer Layer)[] layers) {
        return key => {
            foreach (var item in layers) {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) {
                    return item.Layer;
                }
            }
            return null;
        };
    }

    [TestCase("MBox_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("Ftp_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("Ftps_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("Sftp_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("SftpConnection_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("Zip_Open", "Open", PressKey.Enter, PressModifiers.None, "open")]
    [TestCase("MBox_View", "View", PressKey.P, PressModifiers.Alt, "view toggle=preview")]
    [TestCase("Ftp_View", "View", PressKey.P, PressModifiers.Alt, "view toggle=preview")]
    [TestCase("Ftps_View", "View", PressKey.P, PressModifiers.Alt, "view toggle=preview")]
    [TestCase("Sftp_View", "View", PressKey.P, PressModifiers.Alt, "view toggle=preview")]
    [TestCase("Zip_View", "View", PressKey.P, PressModifiers.Alt, "view toggle=preview")]
    [TestCase("FileSystem_Delete", "Delete", PressKey.Delete, PressModifiers.None, "delete")]
    [TestCase("FileSystem_Delete", "Delete", PressKey.Delete, PressModifiers.Shift, "delete unrecoverable=true")]
    [TestCase("Ftp_Delete", "Delete", PressKey.Delete, PressModifiers.None, "delete")]
    [TestCase("Ftps_Delete", "Delete", PressKey.Delete, PressModifiers.None, "delete")]
    [TestCase("Sftp_Delete", "Delete", PressKey.Delete, PressModifiers.None, "delete")]
    [TestCase("FileSystem_Rename", "Rename", PressKey.F2, PressModifiers.None, "rename")]
    [TestCase("Ftp_Rename", "Rename", PressKey.F2, PressModifiers.None, "rename")]
    [TestCase("Ftps_Rename", "Rename", PressKey.F2, PressModifiers.None, "rename")]
    [TestCase("Sftp_Rename", "Rename", PressKey.F2, PressModifiers.None, "rename")]
    [TestCase("FileSystem_CreateDirectory", "CreateDirectory", PressKey.N,
              PressModifiers.Control | PressModifiers.Shift, "new-dir")]
    [TestCase("Ftp_CreateDirectory", "CreateDirectory", PressKey.N,
              PressModifiers.Control | PressModifiers.Shift, "new-dir")]
    [TestCase("Ftps_CreateDirectory", "CreateDirectory", PressKey.N,
              PressModifiers.Control | PressModifiers.Shift, "new-dir")]
    [TestCase("Sftp_CreateDirectory", "CreateDirectory", PressKey.N,
              PressModifiers.Control | PressModifiers.Shift, "new-dir")]
    [TestCase("Ftp_CreateFile", "CreateFile", PressKey.N,
              PressModifiers.Alt | PressModifiers.Control | PressModifiers.Shift, "new-file")]
    [TestCase("Ftps_CreateFile", "CreateFile", PressKey.N,
              PressModifiers.Alt | PressModifiers.Control | PressModifiers.Shift, "new-file")]
    [TestCase("Sftp_CreateFile", "CreateFile", PressKey.N,
              PressModifiers.Alt | PressModifiers.Control | PressModifiers.Shift, "new-file")]
    [TestCase("Ftp_ViewRaw", "ViewRaw", PressKey.V, PressModifiers.Alt, "raw")]
    [TestCase("Ftps_ViewRaw", "ViewRaw", PressKey.V, PressModifiers.Alt, "raw")]
    public async Task Resolve_LoadedDefaultsShareTheShortcutOfACommandAcrossProviders(string name,
                                                                                      string typeName,
                                                                                      PressKey key,
                                                                                      PressModifiers modifiers,
                                                                                      string defined) {
        var config = await LoadConfig("");
        ICommandTrigger trigger = CommandTrigger.Resolve(name, typeName, Find(config), Translation);

        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger.Triggered(new PressGesture(key, modifiers), out var gesture), Is.True);
        Assert.That(gesture.Defined, Is.EqualTo(defined));
    }

    [TestCase("Ftp_View", "View", "view", "ftp-view")]
    [TestCase("Ftp_Delete", "Delete", "delete", "ftp-delete")]
    [TestCase("Ftp_Rename", "Rename", "rename", "ftp-rename")]
    [TestCase("Ftp_Move", "Move", "move", "ftp-move")]
    [TestCase("Ftp_CreateDirectory", "CreateDirectory", "new-dir", "ftp-new-dir")]
    [TestCase("Ftp_CreateFile", "CreateFile", "new-file", "ftp-new-file")]
    [TestCase("Ftps_View", "View", "view", "ftps-view")]
    [TestCase("Ftps_Delete", "Delete", "delete", "ftps-delete")]
    [TestCase("Ftps_Rename", "Rename", "rename", "ftps-rename")]
    [TestCase("Ftps_Move", "Move", "move", "ftps-move")]
    [TestCase("Ftps_CreateDirectory", "CreateDirectory", "new-dir", "ftps-new-dir")]
    [TestCase("Ftps_CreateFile", "CreateFile", "new-file", "ftps-new-file")]
    [TestCase("Sftp_Open", "Open", "open", "sftp-open")]
    [TestCase("Sftp_View", "View", "view", "sftp-view")]
    [TestCase("Sftp_Delete", "Delete", "delete", "sftp-delete")]
    [TestCase("Sftp_Rename", "Rename", "rename", "sftp-rename")]
    [TestCase("Sftp_Move", "Move", "move", "sftp-move")]
    [TestCase("Sftp_CreateDirectory", "CreateDirectory", "new-dir", "sftp-new-dir")]
    [TestCase("Sftp_CreateFile", "CreateFile", "new-file", "sftp-new-file")]
    public async Task Resolve_LoadedDefaultsGiveRemoteCommandsOnlyTheSharedInput(string name,
                                                                                 string typeName,
                                                                                 string sharedInput,
                                                                                 string providerInput) {
        /*
         * The FTP, FTPS, and SFTP commands take their input from the shared entry of their type
         * name. The defaults no longer define provider-prefixed inputs for them.
         */
        var config = await LoadConfig("");
        ICommandTrigger trigger = CommandTrigger.Resolve(name, typeName, Find(config), Translation);

        Assert.That(trigger, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(config.Cmd.ContainsKey(name), Is.False);
            Assert.That(trigger.Triggered(sharedInput, out _), Is.True);
            Assert.That(trigger.Triggered(providerInput, out _), Is.False);
        }
    }

    [Test]
    public async Task Resolve_LoadedDefaultsDoNotRepeatSharedTriggers() {
        /*
         * The entry of a command's type name, such as cmd[Open], is shared by the commands of that
         * name in every provider. An entry of a provider's command does not repeat what the
         * shared entry holds, and a shortcut that the commands of several providers have in
         * common belongs to the shared entry.
         */
        var config = await LoadConfig("");
        var repeated = new List<string>();
        var shortcuts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in config.Cmd) {
            var separator = pair.Key.IndexOf('_');
            if (separator < 0) {
                continue;
            }
            var typeName = pair.Key.Substring(separator + 1);
            var hasShared = config.Cmd.TryGetValue(typeName, out var shared);
            var gestures = pair.Value.Press.Select(item => ("press", item))
                .Concat(pair.Value.Click.Select(item => ("click", item)));
            foreach (var (kind, item) in gestures) {
                var sharedSet = kind == "press" ? shared?.Press : shared?.Click;
                var gestureIsShared = hasShared && sharedSet.ContainsKey(item.Key);
                if (gestureIsShared) {
                    repeated.Add($"{pair.Key}.{kind}[{item.Key}]");
                }
                var shortcut = $"{typeName}.{kind}[{item.Key}] = {item.Value}";
                if (shortcuts.TryGetValue(shortcut, out var owners) == false) {
                    shortcuts[shortcut] = owners = [];
                }
                owners.Add(pair.Key);
            }
            var sharedInputs = (shared?.Input ?? "").Split('|', StringSplitOptions.TrimEntries);
            foreach (var input in (pair.Value.Input ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)) {
                var inputIsShared = sharedInputs.Contains(input.Trim(), StringComparer.OrdinalIgnoreCase);
                if (inputIsShared) {
                    repeated.Add($"{pair.Key}.input = {input.Trim()}");
                }
            }
        }
        foreach (var pair in shortcuts) {
            var shortcutIsCommon = pair.Value.Count > 1;
            if (shortcutIsCommon) {
                repeated.Add($"{pair.Key} ({string.Join(", ", pair.Value)})");
            }
        }
        Assert.That(repeated, Is.Empty);
    }

    [Test]
    public void Resolve_QualifiedEntryInheritsTheGesturesOfTheUnqualifiedEntry() {
        var find = Find(
            ("Open", new CommandTrigger.Layer(
                "open",
                Set(("ENTER", "*"), ("ENTER+CTRL", "* p")),
                Set(("2L", "*")),
                Set(("open-here", "* here")))),
            ("MBox_Open", new CommandTrigger.Layer("mbox-open", Set(), Set(), Set())));
        ICommandTrigger trigger = CommandTrigger.Resolve("MBox_Open", "Open", find, Translation);

        using (Assert.EnterMultipleScope()) {
            Assert.That(trigger.Triggered("mbox-open", out _), Is.True);
            Assert.That(trigger.Triggered("open", out _), Is.True);
            Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter), out var enter), Is.True);
            Assert.That(enter?.Defined, Is.EqualTo("mbox-open"));
            Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter, PressModifiers.Control), out var ctrl),
                        Is.True);
            Assert.That(ctrl?.Defined, Is.EqualTo("mbox-open p"));
            Assert.That(trigger.Triggered("open-here", out var macro), Is.True);
            Assert.That(macro?.Defined, Is.EqualTo("mbox-open here"));
            Assert.That(trigger.Gestures, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void Resolve_QualifiedEntryInheritsTheInputWhenItHasNone() {
        var find = Find(
            ("Open", new CommandTrigger.Layer("open", Set(("ENTER", "*")), Set(), Set())),
            ("Ftp_Open", new CommandTrigger.Layer(null, Set(), Set(), Set())));
        ICommandTrigger trigger = CommandTrigger.Resolve("Ftp_Open", "Open", find, Translation);

        Assert.That(trigger.Triggered("open", out _), Is.True);
        Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter), out var enter), Is.True);
        Assert.That(enter.Defined, Is.EqualTo("open"));
    }

    [Test]
    public void Resolve_QualifiedEntryOverridesAndDisablesInheritedGestures() {
        var find = Find(
            ("Open", new CommandTrigger.Layer(
                "open",
                Set(("ENTER", "*"), ("ENTER+CTRL", "* p")),
                Set(),
                Set())),
            ("MBox_Open", new CommandTrigger.Layer(
                "mbox-open",
                Set(("ENTER", "* all=true"), ("ENTER+CTRL", "disabled")),
                Set(),
                Set())));
        ICommandTrigger trigger = CommandTrigger.Resolve("MBox_Open", "Open", find, Translation);

        using (Assert.EnterMultipleScope()) {
            Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter), out var enter), Is.True);
            Assert.That(enter?.Defined, Is.EqualTo("mbox-open all=true"));
            Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter, PressModifiers.Control), out _),
                        Is.False);
        }
    }

    [Test]
    public void Resolve_QualifiedEntryEnablesAGestureThatTheUnqualifiedEntryDisables() {
        var find = Find(
            ("Open", new CommandTrigger.Layer("open", Set(("ENTER", "disabled")), Set(), Set())),
            ("MBox_Open", new CommandTrigger.Layer("mbox-open", Set(("ENTER", "*")), Set(), Set())));
        ICommandTrigger trigger = CommandTrigger.Resolve("MBox_Open", "Open", find, Translation);

        Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter), out var enter), Is.True);
        Assert.That(enter.Defined, Is.EqualTo("mbox-open"));
    }

    [Test]
    public void Resolve_UsesTheUnqualifiedEntryAloneWhenThereIsNoQualifiedEntry() {
        var find = Find(("Open", new CommandTrigger.Layer("open", Set(("ENTER", "*")), Set(), Set())));
        ICommandTrigger trigger = CommandTrigger.Resolve("Zip_Open", "Open", find, Translation);

        Assert.That(trigger.Triggered(new PressGesture(PressKey.Enter), out var enter), Is.True);
        Assert.That(enter.Defined, Is.EqualTo("open"));
    }

    [Test]
    public void Resolve_ReturnsNullWhenNeitherEntryExists() {
        Assert.That(CommandTrigger.Resolve("MBox_Open", "Open", Find(), Translation), Is.Null);
    }

    [Test]
    public async Task Compile_DisablesOneGestureFromGroupedDefaultWithoutRemovingSibling() {
        var config = await LoadConfig("cmd[HistoryBack].press[ALT+LEFT] = disabled\r\n");
        var historyBack = config.Cmd["HistoryBack"];
        ICommandTrigger trigger = CommandTrigger.Compile(
            historyBack.Input,
            historyBack.Press,
            historyBack.Click,
            historyBack.Macro,
            Translation);
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
        ICommandTrigger trigger = CommandTrigger.Compile("terminal", press, [], [], Translation);
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
        ICommandTrigger trigger = CommandTrigger.Compile("terminal|term", [], [], macro, Translation);

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
