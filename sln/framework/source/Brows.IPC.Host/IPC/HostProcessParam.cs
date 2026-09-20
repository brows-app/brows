using Brows.Config;
using Domore.Logs;
using System;
using System.IO;

namespace Brows.IPC;

/// <summary>
/// A parameter for the host process.
/// </summary>
public sealed class HostProcessParam {
    private Environment.SpecialFolder GetFolder(string name, ConfigKind? kind = null) {
        var k = kind ?? Config;
        return k switch {
            ConfigKind.Common => Environment.SpecialFolder.CommonApplicationData,
            _ => Environment.SpecialFolder.LocalApplicationData
        };
    }

    internal string GetIPCRoot(string name, ConfigKind? kind = null) {
        /*
         * All Brows IPC host processes will read/write data in a directory descended from a
         * common root. If you're looking for IPC data, look in one of these:
         *  - %PROGRAMDATA%\Brows\IPC
         *  - %LOCALAPPDATA%\Brows\IPC
         */
        return Path.Combine(
            Environment.GetFolderPath(GetFolder(name, kind), Environment.SpecialFolderOption.DoNotVerify),
            "Brows",
            "IPC");
    }

    internal string GetNameRoot(string name, ConfigKind? kind = null) {
        var n = (name ?? "_").Trim();
        foreach (var c in Path.GetInvalidPathChars()) {
            n = n.Replace(c, '_');
        }
        return Path.Combine(GetIPCRoot(name, kind), n);
    }

    internal string GetHostRoot(string name, ConfigKind? kind = null) {
        return Path.Combine(GetNameRoot(name, kind), "Host");
    }

    /// <summary>
    /// Gets or sets the kind of configuration to use for the process.
    /// </summary>
    public ConfigKind? Config { get; set; }

    /// <summary>
    /// Gets or sets the logging threshold of the file logger for the process.
    /// </summary>
    public LogSeverity? LogFileThreshold { get; set; }
}
