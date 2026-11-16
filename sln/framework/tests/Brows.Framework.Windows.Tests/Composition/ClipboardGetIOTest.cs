using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Composition;

[TestFixture]
internal sealed class ClipboardGetIOTest {
    private sealed class TestClipboardGetIOData : IClipboardGetIOData {
        public bool MoveOnPaste { get; set; }
    }

    [Test]
    public async Task Work_ReportsAndConsumesMoveOnPasteData() {
        var transport = new ClipboardTestTransport {
            PreferredDropEffect = DragDropEffects.Move,
            HasFileDropList = true
        };
        transport.Files.Add("prepared.txt");
        transport.OtherFormats["unrelated"] = new object();
        var collection = new List<IProvidedIO>();
        var data = new TestClipboardGetIOData();

        var worked = await new ClipboardGetIO(transport).Work(
            collection,
            data,
            null,
            CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.True);
            Assert.That(data.MoveOnPaste, Is.True);
            Assert.That(collection.Count, Is.EqualTo(1));
            Assert.That(transport.Files.Count, Is.EqualTo(0));
            Assert.That(transport.OtherFormats, Is.Empty);
            Assert.That(transport.PreferredDropEffect, Is.EqualTo(DragDropEffects.Copy));
        }
    }

    [Test]
    public async Task Work_MoveClearKeepsPublishedFileAvailableForPaste() {
        var directory = Path.Combine(Path.GetTempPath(), "BrowsClipboardGetIOTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "prepared.txt");
        File.WriteAllText(path, "prepared contents");
        try {
            var transport = new ClipboardTestTransport {
                PreferredDropEffect = DragDropEffects.Move,
                HasFileDropList = true
            };
            transport.Files.Add(path);
            var collection = new List<IProvidedIO>();
            var data = new TestClipboardGetIOData();

            var worked = await new ClipboardGetIO(transport).Work(
                collection,
                data,
                null,
                CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(worked, Is.True);
                Assert.That(collection.FileSources(), Is.EqualTo(new[] { path }));
                Assert.That(File.ReadAllText(path), Is.EqualTo("prepared contents"));
            }
        }
        finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
