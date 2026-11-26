using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Brows.Updates;

internal sealed class UpdateInstallation {
    private static UpdateInstallation Create() {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return new(
            version?.InformationalVersion,
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.OSArchitecture,
            Path.GetDirectoryName(Environment.ProcessPath),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brows"));
    }

    internal static UpdateInstallation Current => field ??= Create();

    internal string VersionText { get; }
    internal AppVersion Version { get; }
    internal string Architecture { get; }
    internal Architecture OSArchitecture { get; }
    internal string ProgramDirectory { get; }
    internal string Root { get; }
    internal string AppDirectory => Path.Combine(Root, "app");
    internal string UpdateDirectory => Path.Combine(Root, "update");

    internal bool Installable => Version is not null && Architecture is not null
        && string.Equals(ProgramDirectory, AppDirectory, StringComparison.OrdinalIgnoreCase);

    internal UpdateInstallation(string versionText, Architecture process, Architecture os,
                                string programDirectory, string root) {
        VersionText = AppVersion.RemoveMetadata(versionText);
        _ = AppVersion.TryParse(VersionText, out var version);
        Version = version;
        Architecture = AppArchitecture.From(process);
        OSArchitecture = os;
        ProgramDirectory = programDirectory is null ? null : Normalize(programDirectory);
        Root = Normalize(root);

        static string Normalize(string path) {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
