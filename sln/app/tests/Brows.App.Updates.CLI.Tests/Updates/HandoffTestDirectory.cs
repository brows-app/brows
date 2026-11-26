using System.IO;

namespace Brows.Updates;

internal sealed class HandoffTestDirectory : IDisposable {
    internal string Root { get; } = Directory.CreateTempSubdirectory("brows handoff = O'Brien-").FullName;
    internal string Updates => Path.Combine(Root, "update");
    internal string Launch => Path.Combine(Root, "brows.exe");
    internal string Log => Path.Combine(Updates, Version, "install.log");
    internal string Outcome => Path.Combine(Updates, "outcome.json");
    internal string Msi => Path.Combine(Updates, Version, $"brows-x64-{Version}.msi");
    internal string Version => "0.103.0.62";

    internal HandoffTestDirectory() {
        Directory.CreateDirectory(Path.GetDirectoryName(Log));
    }

    internal string[] Properties() {
        return [
            "pid=123", $"msi=\"{Msi}\"", $"log=\"{Log}\"", $"outcome=\"{Outcome}\"",
            $"version={Version}", $"updates=\"{Updates}\"", $"launch=\"{Launch}\""
        ];
    }

    internal UpdaterArguments Parse() {
        Assert.That(UpdaterArguments.TryParse(string.Join(' ', Properties()), out var parsed), Is.True);
        return parsed;
    }

    void IDisposable.Dispose() {
        Directory.Delete(Root, recursive: true);
    }
}
