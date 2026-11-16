using System.IO;
using System.Text;

namespace Brows;

internal sealed class UpdateTestSandbox : IDisposable {
    private readonly string Marker;

    private bool Disposed;

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
            var isOrdinaryReadOnlyFile =
                (attributes & FileAttributes.ReparsePoint) == 0 &&
                (attributes & FileAttributes.ReadOnly) != 0;
            if (isOrdinaryReadOnlyFile) {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            File.Delete(file);
        }
        Directory.Delete(directory, recursive: false);
    }

    public string Root { get; }

    public UpdateTestSandbox() {
        Root = Path.Combine(Path.GetTempPath(), "Brows.Zip.Async.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Marker = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(Root, ".brows-zip-update-test-root"), Marker, new UTF8Encoding(false));
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

    public void Dispose() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        var fullRoot = Path.GetFullPath(Root);
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.Zip.Async.Tests"));
        var relative = Path.GetRelativePath(tempRoot, fullRoot);
        var isTempRoot = relative == ".";
        var escapesTempRoot = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);
        var outside = isTempRoot || escapesTempRoot;
        if (outside) {
            throw new InvalidOperationException("Refusing to remove a path outside the test temporary directory.");
        }
        var rootAttributes = File.GetAttributes(fullRoot);
        var rootIsReparsePoint = (rootAttributes & FileAttributes.ReparsePoint) != 0;
        if (rootIsReparsePoint) {
            throw new InvalidOperationException("Refusing to remove a reparse-point test directory.");
        }
        var markerPath = Path.Combine(fullRoot, ".brows-zip-update-test-root");
        var markerExists = File.Exists(markerPath);
        var markerMatches = false;
        if (markerExists) {
            markerMatches = string.Equals(File.ReadAllText(markerPath), Marker, StringComparison.Ordinal);
        }
        var markerIsInvalid = markerExists == false || markerMatches == false;
        if (markerIsInvalid) {
            throw new InvalidOperationException("Refusing to remove a directory without its ownership marker.");
        }
        DeleteTree(fullRoot);
    }

}
