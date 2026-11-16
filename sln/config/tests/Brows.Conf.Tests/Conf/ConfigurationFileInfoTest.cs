using Brows.Conf.ConfigurationFileInfos;
using Domore.Conf.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationFileInfoTest {
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

    private static async Task InTemporaryDirectory(Func<string, Task> test) {
        var temporaryDirectory = Directory.CreateTempSubdirectory("BrowsConfigurationFileInfoTest-");
        var directory = Path.GetFullPath(temporaryDirectory.FullName);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var temporaryRootPrefix = Path.EndsInDirectorySeparator(temporaryRoot)
            ? temporaryRoot
            : temporaryRoot + Path.DirectorySeparatorChar;
        var directoryIsInTemp = directory.StartsWith(temporaryRootPrefix, StringComparison.OrdinalIgnoreCase);
        try {
            Assert.That(directoryIsInTemp, Is.True);
            await test(directory);
        }
        finally {
            if (directoryIsInTemp) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Configure_UserFileWithOnlyOverridesKeepsOtherDefaults() {
        await InTemporaryDirectory(async directory => {
            var defaultDirectory = Directory.CreateDirectory(Path.Combine(directory, "default"));
            var defaultFile = Path.Combine(defaultDirectory.FullName, "override.conf.default");
            await File.WriteAllTextAsync(
                defaultFile,
                "#\r\n" +
                "# WARNING: This file will be overwritten when a new version of the application is installed.\r\n" +
                "#\r\n" +
                "\r\n" +
                "Name = shipped\r\n" +
                "Color = red\r\n" +
                "First[0] = a\r\n" +
                "First[1] = b\r\n" +
                "Files = x, y\r\n" +
                "Editor[*] = notepad.exe\r\n",
                Encoding.UTF8);
            var userFile = Path.Combine(directory, "override.conf");
            await File.WriteAllTextAsync(
                userFile,
                "Color = blue\r\n" +
                "First[1] = c\r\n" +
                "Editor[.txt] = code.exe\r\n",
                Encoding.UTF8);

            var info = new TypedConfigurationFileInfo<OverrideConfig>(userFile, defaultFile, "brows");

            var config = await info.Configure(CancellationToken.None);

            Assert.That(config.Name, Is.EqualTo("shipped"));
            Assert.That(config.Color, Is.EqualTo("blue"));
            Assert.That(config.First, Is.EqualTo(new[] { "a", "c" }));
            Assert.That(config.Files, Is.EqualTo(new[] { "x", "y" }));
            Assert.That(config.Editor, Has.Count.EqualTo(2));
            Assert.That(config.Editor["*"], Is.EqualTo("notepad.exe"));
            Assert.That(config.Editor[".txt"], Is.EqualTo("code.exe"));
        });
    }

    [Test]
    public async Task NamedConfigure_AppliesDefaultFileThenUserFile() {
        await InTemporaryDirectory(async directory => {
            var defaultDirectory = Directory.CreateDirectory(Path.Combine(directory, "default"));
            var defaultFile = Path.Combine(defaultDirectory.FullName, "named.conf.default");
            await File.WriteAllTextAsync(
                defaultFile,
                "Name = shipped\r\n" +
                "Color = red\r\n",
                Encoding.UTF8);
            var userFile = Path.Combine(directory, "named.conf");
            await File.WriteAllTextAsync(userFile, "Color = blue\r\n", Encoding.UTF8);
            var info = new NamedConfigurationFileInfo(userFile, defaultFile, "brows");
            var config = new OverrideConfig();
            var applied = new List<string>();

            await info.Configure(conf => {
                conf.Configure(config, key: "");
                applied.Add(config.Color);
            }, CancellationToken.None);

            Assert.That(applied, Is.EqualTo(new[] { "red", "blue" }));
            Assert.That(config.Name, Is.EqualTo("shipped"));
            Assert.That(config.Color, Is.EqualTo("blue"));
        });
    }

    [Test]
    public async Task NamedConfigure_MissingUserFileAppliesDefaultFile() {
        await InTemporaryDirectory(async directory => {
            var defaultFile = Path.Combine(directory, "named.conf.default");
            await File.WriteAllTextAsync(defaultFile, "Name = shipped\r\n", Encoding.UTF8);
            var info = new NamedConfigurationFileInfo(Path.Combine(directory, "named.conf"), defaultFile, "brows");
            var config = new OverrideConfig();

            await info.Configure(conf => conf.Configure(config, key: ""), CancellationToken.None);

            Assert.That(config.Name, Is.EqualTo("shipped"));
        });
    }

    [Test]
    public async Task Configure_UserEmptyGestureValueDoesNotRemoveShippedDefaultShortcut() {
        var temporaryDirectory = Directory.CreateTempSubdirectory("BrowsConfigurationFileInfoTest-");
        var directory = Path.GetFullPath(temporaryDirectory.FullName);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var temporaryRootPrefix = Path.EndsInDirectorySeparator(temporaryRoot)
            ? temporaryRoot
            : temporaryRoot + Path.DirectorySeparatorChar;
        var directoryIsInTemp = directory.StartsWith(temporaryRootPrefix, StringComparison.OrdinalIgnoreCase);
        try {
            Assert.That(directoryIsInTemp, Is.True);
            var userFile = Path.Combine(directory, "trigger.conf");
            await File.WriteAllTextAsync(
                userFile,
                "cmd[FileSystem_TerminalRelease].input = terminal-return\r\n" +
                "cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F12] =\r\n" +
                "cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F11] = *\r\n");

            var info = new TypedConfigurationFileInfo<TriggerConfig>(userFile, DefaultTriggerFile(), "brows");

            var config = await info.Configure(CancellationToken.None);

            Assert.That(config.Cmd["FileSystem_TerminalOpen"].Input, Is.EqualTo("terminal|term"));
            Assert.That(config.Cmd["FileSystem_TerminalRelease"].Input, Is.EqualTo("terminal-return"));
            Assert.That(config.Cmd["FileSystem_TerminalOpen"].Press["CTRL+ALT+T"], Is.EqualTo("*"));
            Assert.That(
                config.Cmd["FileSystem_TerminalRelease"].Press["CTRL+SHIFT+F11"],
                Is.EqualTo("*"));
            Assert.That(
                config.Cmd["FileSystem_TerminalRelease"].Press["CTRL+SHIFT+F12"],
                Is.EqualTo("*"));
        }
        finally {
            if (directoryIsInTemp) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class OverrideConfig {
        public string Name { get; set; }
        public string Color { get; set; }
        public List<string> First { get; set; } = [];

        [ConfListItems]
        public List<string> Files { get; set; } = [];

        public Dictionary<string, string> Editor { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TriggerConfig {
        public Dictionary<string, TriggerCommand> Cmd { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TriggerCommand {
        public string Input { get; set; }

        public Dictionary<string, string> Press { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
