using System;
using System.IO;
using System.Text;

namespace Brows;

/// <summary>
/// Holds a folder of the file system that is used by a test. The folder and
/// everything that is inside of it are removed when the sandbox is disposed.
/// </summary>
internal sealed class SevenZipTestSandbox : IDisposable {
    private readonly string Marker;
    private bool Disposed;

    public SevenZipTestSandbox() {
        Root = Path.Combine(Path.GetTempPath(), "Brows.SevenZip.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Marker = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(Root, ".brows-sevenzip-test-root"), Marker, new UTF8Encoding(false));
    }

    /// <summary>
    /// Gets the folder that holds the files of the test.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the location of a file of the test. The file does not have to exist.
    /// </summary>
    public string PathFor(string name) {
        return Path.Combine(Root, name.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Writes a file of the test and returns its location.
    /// </summary>
    public string WriteFile(string name, string contents) {
        var path = PathFor(name);
        var directory = Path.GetDirectoryName(path);
        if (directory != null) {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Creates a folder of the test and returns its location.
    /// </summary>
    public string CreateDirectory(string name) {
        var path = PathFor(name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        var fullRoot = Path.GetFullPath(Root);
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.SevenZip.Tests"));
        var relative = Path.GetRelativePath(tempRoot, fullRoot);
        var outside = relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);
        if (outside) {
            throw new InvalidOperationException("Refusing to remove a path outside of the test temporary directory.");
        }
        var markerPath = Path.Combine(fullRoot, ".brows-sevenzip-test-root");
        if (File.Exists(markerPath) == false ||
            string.Equals(File.ReadAllText(markerPath), Marker, StringComparison.Ordinal) == false) {
            throw new InvalidOperationException("Refusing to remove a directory without its ownership marker.");
        }
        DeleteTree(fullRoot);
    }

    private static void DeleteTree(string directory) {
        foreach (var child in Directory.EnumerateDirectories(directory)) {
            var attributes = File.GetAttributes(child);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                Directory.Delete(child, recursive: false);
            }
            else {
                DeleteTree(child);
            }
        }
        foreach (var file in Directory.EnumerateFiles(directory)) {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReparsePoint) == 0 && (attributes & FileAttributes.ReadOnly) != 0) {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            File.Delete(file);
        }
        Directory.Delete(directory, recursive: false);
    }
}
