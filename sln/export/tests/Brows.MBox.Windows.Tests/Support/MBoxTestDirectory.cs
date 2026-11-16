using System;
using System.IO;

namespace Brows.Support;

/// <summary>
/// Holds a directory that holds the files of a test, and removes it when the test is done.
/// </summary>
internal sealed class MBoxTestDirectory : IDisposable {
    private bool _Disposed;

    /// <summary>
    /// Gets the directory that holds the files of the test.
    /// </summary>
    public DirectoryInfo Directory {
        get;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxTestDirectory"/> class.
    /// </summary>
    public MBoxTestDirectory() {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Brows",
            "MBoxWindowsTests",
            Guid.NewGuid().ToString("N"));
        Directory = System.IO.Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Removes the directory of the test.
    /// </summary>
    public void Dispose() {
        if (_Disposed) {
            return;
        }
        _Disposed = true;
        try {
            Directory.Delete(recursive: true);
        }
        catch (IOException) {
        }
        catch (UnauthorizedAccessException) {
        }
    }
}
