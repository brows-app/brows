using System;
using System.IO;

namespace Brows.Commands;

internal sealed class MBoxExportPublisher {
    private readonly Action<string, string, bool> _BeforePublish;

    internal MBoxExportPublisher(Action<string, string, bool> beforePublish = null) {
        _BeforePublish = beforePublish;
    }

    internal void Publish(string staging, string destination, bool overwrite) {
        _BeforePublish?.Invoke(staging, destination, overwrite);
        var destinationExists = File.Exists(destination);
        var canReplace = overwrite && destinationExists;
        if (canReplace) {
            File.Replace(staging, destination, destinationBackupFileName: null);
            return;
        }
        File.Move(staging, destination);
    }
}
