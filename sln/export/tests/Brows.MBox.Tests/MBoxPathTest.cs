using System.IO;

namespace Brows;

[TestFixture]
internal sealed class MBoxPathTest {
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void IsMailbox_ReturnsFalseForAPathThatNamesNothing(string path) {
        Assert.That(MBoxPath.IsMailbox(path), Is.False);
    }

    [TestCase("inbox.mbox")]
    [TestCase("inbox.MBOX")]
    [TestCase("inbox.Mbox")]
    [TestCase(@"C:\Mail\inbox.mbox")]
    public void IsMailbox_ReturnsTrueForAMailbox(string path) {
        Assert.That(MBoxPath.IsMailbox(path), Is.True);
    }

    [TestCase("inbox.mbox.gz")]
    [TestCase("inbox.MBOX.GZ")]
    public void IsMailbox_ReturnsFalseForACompressedMailbox(string path) {
        Assert.That(MBoxPath.IsMailbox(path), Is.False);
    }

    [TestCase("inbox.txt")]
    [TestCase("mbox")]
    [TestCase("inbox.mbox.zip")]
    public void IsMailbox_ReturnsFalseForAFileThatIsNotAMailbox(string path) {
        Assert.That(MBoxPath.IsMailbox(path), Is.False);
    }

    [Test]
    public void MessageName_NamesTheFileAfterTheOffsetOfTheMessage() {
        using (Assert.EnterMultipleScope()) {
            Assert.That(MBoxPath.MessageName(0), Is.EqualTo("message-0.eml"));
            Assert.That(MBoxPath.MessageName(1024), Is.EqualTo("message-1024.eml"));
        }
    }

    [Test]
    public void MessageName_UsesTheOffsetOfTheInvariantCulture() {
        var culture = CultureInfo.CurrentCulture;
        try {
            /*
             * The culture of the machine writes a negative number with a sign that is not the
             * one of the invariant culture, so the name of the file would differ from it.
             */
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Assert.That(MBoxPath.MessageName(-1), Is.EqualTo("message--1.eml"));
        }
        finally {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Test]
    public void ArchiveName_NamesTheArchiveAfterTheOffsetOfTheMessage() {
        using (Assert.EnterMultipleScope()) {
            Assert.That(MBoxPath.ArchiveName(0), Is.EqualTo("message-0.zip"));
            Assert.That(MBoxPath.ArchiveName(1024), Is.EqualTo("message-1024.zip"));
        }
    }

    [Test]
    public void SessionDirectory_LiesUnderTheDirectoryOfTheFeature() {
        var directory = MBoxPath.SessionDirectory("01234567");
        Assert.That(directory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "Brows", "MBox", "01234567")));
    }
}
