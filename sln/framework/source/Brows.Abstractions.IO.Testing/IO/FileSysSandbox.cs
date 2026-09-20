using System;
using System.IO;
using System.Threading;

namespace Brows.IO;

public sealed class FileSysSandbox : IDisposable {
    private readonly string TempDir;

    private void Dispose(bool disposing) {
        try {
            var i = 0;
            for (; ; ) {
                try {
                    if (Directory.Exists(TempDir)) {
                        Directory.Delete(TempDir, recursive: true);
                    }
                    return;
                }
                catch {
                    if (++i > 2) {
                        throw;
                    }
                }
                Thread.Sleep(100);
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }

    public string Root => TempDir;

    public FileSysSandbox() {
        TempDir = Path.Combine(Path.GetTempPath(), $"{GetType().FullName}_{Path.GetRandomFileName()}");
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~FileSysSandbox() {
        Dispose(false);
    }
}
