using System.IO;
using System.Text;

namespace Brows.Commands;

internal sealed class ZipCommandTestSandbox : IDisposable {
    private readonly string Marker;
    private bool Disposed;

    public string Root { get; }

    public ZipCommandTestSandbox() {
        Root = Path.Combine(Path.GetTempPath(), "Brows.FileSystem.Commands.Zip.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Marker = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(Root, ".brows-zip-command-test-root"), Marker, new UTF8Encoding(false));
    }

    public string PathFor(string name) {
        return Path.Combine(Root, name.Replace('/', Path.DirectorySeparatorChar));
    }

    public string WriteFile(string name, string contents) {
        var path = PathFor(name);
        var directory = Path.GetDirectoryName(path);
        if (directory is not null) {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

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
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.FileSystem.Commands.Zip.Tests"));
        var relative = Path.GetRelativePath(tempRoot, fullRoot);
        var sandboxPathIsOutsideTemporaryRoot =
            relative == "." ||
            relative.StartsWith("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative);
        if (sandboxPathIsOutsideTemporaryRoot) {
            throw new InvalidOperationException("Refusing to remove a path outside the test temporary directory.");
        }
        var markerPath = Path.Combine(fullRoot, ".brows-zip-command-test-root");
        var markerExists = File.Exists(markerPath);
        if (markerExists is false) {
            throw new InvalidOperationException("Refusing to remove a directory without its ownership marker.");
        }
        var markerMatches = string.Equals(File.ReadAllText(markerPath), Marker, StringComparison.Ordinal);
        if (markerMatches is false) {
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
            var fileIsReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
            var fileIsReadOnly = (attributes & FileAttributes.ReadOnly) != 0;
            var fileCanBeMarkedWritable = fileIsReparsePoint is false && fileIsReadOnly;
            if (fileCanBeMarkedWritable) {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            File.Delete(file);
        }
        Directory.Delete(directory, recursive: false);
    }
}
