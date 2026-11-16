using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed partial class ClipboardSetIOTest {
    [TestCase("name ")]
    [TestCase("name.")]
    [TestCase("folder /file.txt")]
    [TestCase("folder./file.txt")]
    public async Task Work_PreservesTheClipboardForWindowsPathAliases(string relativePath) {
        var externalPath = Path.Combine(Path.GetTempPath(), "clipboard-external-sentinel.txt");
        Transport.Files.Add(externalPath);
        Transport.HasFileDropList = true;
        var source = new TestStreamSource(relativePath, "archive contents");
        var copied = await new ClipboardSetIO(Transport).Work(new[] {
            new ProvidedIO { StreamSets = new[] { new TestStreamSet(new[] { source }) } }
        }, new TestClipboardSetIOData(), null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.False);
            Assert.That(Transport.Files.Cast<string>(), Is.EqualTo(new[] { externalPath }));
            Assert.That(Transport.PublishFileDropListCount, Is.Zero);
            Assert.That(Transport.ClearCount, Is.Zero);
        }
    }
}
