using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class TarProviderTest {
    private sealed class TestPanelDrop : IPanelDrop {
        public object Target => null;
        public IReadOnlyList<string> CopyFiles { get; }
        public IReadOnlyList<string> MoveFiles => Array.Empty<string>();

        public TestPanelDrop(IReadOnlyList<string> copyFiles) {
            CopyFiles = copyFiles;
        }
    }

    [Test]
    public async Task Drop_CopiesFilesIntoTheCurrentArchiveDirectoryAndPreservesSameNameRootEntry() {
        var archivePath = Sandbox.PathFor("target.tar");
        var rootSource = Sandbox.WriteFile("root-notes.txt", "root contents");
        var existingSource = Sandbox.WriteFile("existing.txt", "existing contents");
        var folderSource = Sandbox.WriteFile("folder-notes.txt", "old folder contents");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "notes.txt", rootSource },
            { "docs/existing.txt", existingSource },
            { "docs/notes.txt", folderSource }
        }, null, CancellationToken.None);
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse($"tar:1|f={archivePath}|d=docs", factory, CancellationToken.None);
        Assert.That(archive, Is.Not.Null);
        try {
            var provider = new TarProvider(archive, factory);
            var dropSource = Sandbox.WriteFile("drop-source/notes.txt", "dropped contents");
            var dropped = await ((IProvider)provider).Drop(
                new TestPanelDrop(new[] { dropSource }),
                null,
                CancellationToken.None);

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var rootPath = Sandbox.PathFor("root-copy.txt");
            var droppedPath = Sandbox.PathFor("dropped-copy.txt");
            await items.Items.Single(item => item.Name.Original == "notes.txt")
                .Extract(rootPath, overwrite: false, CancellationToken.None);
            await items.Items.Single(item => item.Name.Original == "docs/notes.txt")
                .Extract(droppedPath, overwrite: false, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(dropped, Is.True);
                Assert.That(File.ReadAllText(rootPath), Is.EqualTo("root contents"));
                Assert.That(File.ReadAllText(droppedPath), Is.EqualTo("dropped contents"));
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task Drop_RecursivelyCopiesDirectoryFilesAndEmptyDescendants() {
        var archivePath = Sandbox.PathFor("target.tar");
        var existingSource = Sandbox.WriteFile("existing.txt", "existing contents");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "docs/existing.txt", existingSource }
        }, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("drop-source");
        Sandbox.WriteFile("drop-source/nested/file.txt", "nested contents");
        Sandbox.CreateDirectory("drop-source/empty/descendant");
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse($"tar:1|f={archivePath}|d=docs", factory, CancellationToken.None);
        Assert.That(archive, Is.Not.Null);
        try {
            var provider = new TarProvider(archive, factory);
            var dropped = await ((IProvider)provider).Drop(
                new TestPanelDrop(new[] { sourceDirectory }),
                null,
                CancellationToken.None);

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var file = items.Items.Single(item => item.Name.Original == "docs/drop-source/nested/file.txt");
            var extractedFile = Sandbox.PathFor("nested-copy.txt");
            await file.Extract(extractedFile, overwrite: false, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(dropped, Is.True);
                Assert.That(File.ReadAllText(extractedFile), Is.EqualTo("nested contents"));
                Assert.That(items.Items.Select(item => item.Name.Original),
                    Does.Contain("docs/drop-source/empty/descendant"));
            }
        }
        finally {
            archive.Release();
        }
    }
}
