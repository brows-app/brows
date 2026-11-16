using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
internal sealed class SevenZipInputTest {
    [Test]
    public void Entries_IncludesASelectedEmptyDirectory() {
        using var sandbox = new InputTestDirectory();
        var empty = Directory.CreateDirectory(Path.Combine(sandbox.Root, "empty"));
        var input = new SevenZipInput(true, new FileSystemInfo[] { empty });
        var entries = input.Entries(null, CancellationToken.None).ToList();

        using (Assert.EnterMultipleScope()) {
            Assert.That(entries.Select(entry => entry.EntryName), Is.EquivalentTo(new[] { "empty" }));
            Assert.That(entries.Single().FilePath, Is.EqualTo(empty.FullName));
        }
    }

    [Test]
    public void Entries_IncludesEmptyDescendantsOfASelectedDirectory() {
        using var sandbox = new InputTestDirectory();
        var selected = Directory.CreateDirectory(Path.Combine(sandbox.Root, "selected"));
        Directory.CreateDirectory(Path.Combine(selected.FullName, "empty-child"));
        File.WriteAllText(Path.Combine(selected.FullName, "file.txt"), "content");
        var input = new SevenZipInput(true, new FileSystemInfo[] { selected });
        var entries = input.Entries(null, CancellationToken.None).ToList();

        var emptyChild = entries.Single(entry => entry.EntryName == "empty-child");
        Assert.That(emptyChild.FilePath, Is.EqualTo(Path.Combine(selected.FullName, "empty-child")));
    }

    private sealed class InputTestDirectory : IDisposable {
        private readonly string Marker;

        public string Root { get; }

        public InputTestDirectory() {
            var parent = Path.Combine(Path.GetTempPath(), "Brows.SevenZip.Input");
            Directory.CreateDirectory(parent);
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Marker = Guid.NewGuid().ToString("N");
            File.WriteAllText(
                Path.Combine(Root, ".brows-sevenzip-input-test-root"),
                Marker,
                new UTF8Encoding(false));
        }

        public void Dispose() {
            var fullRoot = Path.GetFullPath(Root);
            var tempParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.SevenZip.Input"));
            var relative = Path.GetRelativePath(tempParent, fullRoot);
            var outside = relative == "." ||
                          relative == ".." ||
                          relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                          Path.IsPathRooted(relative);
            if (outside) {
                throw new InvalidOperationException("Refusing to remove a path outside the test temporary directory.");
            }
            var attributes = File.GetAttributes(fullRoot);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidOperationException("Refusing to remove a reparse point as a test directory.");
            }
            var marker = Path.Combine(fullRoot, ".brows-sevenzip-input-test-root");
            if (File.Exists(marker) == false || File.ReadAllText(marker) != Marker) {
                throw new InvalidOperationException(
                    "Refusing to remove a test directory without its ownership marker.");
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
}
