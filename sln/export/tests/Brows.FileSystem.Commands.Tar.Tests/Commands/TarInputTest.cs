using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
internal sealed class TarInputTest {
    [Test]
    public void Entries_IncludesASelectedEmptyDirectory() {
        using var sandbox = new InputTestDirectory();
        var empty = Directory.CreateDirectory(Path.Combine(sandbox.Root, "empty"));
        var input = new TarInput(true, new FileSystemInfo[] { empty });
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
        var input = new TarInput(true, new FileSystemInfo[] { selected });
        var entries = input.Entries(null, CancellationToken.None).ToList();

        var emptyChild = entries.Single(entry => entry.EntryName == "empty-child");
        Assert.That(emptyChild.FilePath, Is.EqualTo(Path.Combine(selected.FullName, "empty-child")));
    }

    [TestCase("child/deeper/file.txt", false)]
    [TestCase("child/file.txt", false)]
    [TestCase("child/deeper/file.txt", true)]
    public void Entries_PreservesPathsBelowASingleSelectedDirectory(string relativePath, bool commonSubtree) {
        using var sandbox = new InputTestDirectory();
        var directory = Directory.CreateDirectory(Path.Combine(sandbox.Root, "selected"));
        var file = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "contents");
        if (commonSubtree) {
            var peerDirectory = Path.GetDirectoryName(file)!;
            File.WriteAllText(Path.Combine(peerDirectory, "peer.txt"), "peer");
        }
        var input = new TarInput(true, new FileSystemInfo[] { directory });

        var entry = input.Entries(null, CancellationToken.None).Single(item => item.FilePath == file);

        Assert.That(entry.EntryName, Is.EqualTo(relativePath));
    }

    [Test]
    public void Entries_MixedSelectionsUseTheirCommonParent() {
        using var sandbox = new InputTestDirectory();
        var selected = Directory.CreateDirectory(Path.Combine(sandbox.Root, "selected"));
        var deeper = Path.Combine(selected.FullName, "child", "deeper");
        Directory.CreateDirectory(deeper);
        File.WriteAllText(Path.Combine(deeper, "file.txt"), "contents");
        var loose = Path.Combine(sandbox.Root, "loose.txt");
        File.WriteAllText(loose, "loose contents");
        var input = new TarInput(true, new FileSystemInfo[] {
            selected,
            new FileInfo(loose)
        });

        var entries = input.Entries(null, CancellationToken.None).Select(entry => entry.EntryName).ToList();

        Assert.That(entries, Is.EquivalentTo(new[] {
            "selected/child/deeper/file.txt",
            "loose.txt"
        }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Entries_MultipleSelectedDirectoriesPreserveTheirNamesAndSubtrees(bool secondContainsFiles) {
        using var sandbox = new InputTestDirectory();
        var first = Directory.CreateDirectory(Path.Combine(sandbox.Root, "first"));
        var second = Directory.CreateDirectory(Path.Combine(sandbox.Root, "second"));
        Directory.CreateDirectory(Path.Combine(first.FullName, "shared", "deeper"));
        File.WriteAllText(Path.Combine(first.FullName, "shared", "deeper", "file.txt"), "first contents");
        Directory.CreateDirectory(Path.Combine(first.FullName, "empty", "deeper"));
        if (secondContainsFiles) {
            Directory.CreateDirectory(Path.Combine(second.FullName, "other"));
            File.WriteAllText(Path.Combine(second.FullName, "other", "file.txt"), "second contents");
        }
        var input = new TarInput(true, new FileSystemInfo[] {
            first,
            second
        });

        var entries = input.Entries(null, CancellationToken.None).Select(entry => entry.EntryName).ToList();

        Assert.That(entries, Is.EquivalentTo(new[] {
            "first/shared/deeper/file.txt",
            "first/empty/deeper",
            secondContainsFiles ? "second/other/file.txt" : "second"
        }));
    }

    private sealed class InputTestDirectory : IDisposable {
        private readonly string Marker;

        public string Root { get; }

        public InputTestDirectory() {
            var parent = Path.Combine(Path.GetTempPath(), "Brows.Tar.Input");
            Directory.CreateDirectory(parent);
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Marker = Guid.NewGuid().ToString("N");
            File.WriteAllText(
                Path.Combine(Root, ".brows-tar-input-test-root"),
                Marker,
                new UTF8Encoding(false));
        }

        public void Dispose() {
            var fullRoot = Path.GetFullPath(Root);
            var tempParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.Tar.Input"));
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
            var marker = Path.Combine(fullRoot, ".brows-tar-input-test-root");
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
