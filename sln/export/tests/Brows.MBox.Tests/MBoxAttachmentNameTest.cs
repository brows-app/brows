using System.Collections.Generic;

namespace Brows;

[TestFixture]
internal sealed class MBoxAttachmentNameTest {
    [TestCase("report.pdf", "report.pdf")]
    [TestCase(@"..\..\evil.txt", "evil.txt")]
    [TestCase("sub/dir.txt", "dir.txt")]
    [TestCase("CON.txt", "_CON.txt")]
    [TestCase("trail. ", "trail")]
    [TestCase("a:b?.txt", "a_b_.txt")]
    public void FileName_ReturnsASafeName(string fileName, string expected) {
        Assert.That(MBoxAttachmentName.FileName(fileName), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" . ")]
    [TestCase("folder/")]
    public void FileName_NamesAnUnnamedAttachment(string fileName) {
        Assert.That(MBoxAttachmentName.FileName(fileName), Is.EqualTo("attachment"));
    }

    [Test]
    public void FileName_ShortensALongNameAndKeepsItsExtension() {
        var name = MBoxAttachmentName.FileName(new string('a', 300) + ".pdf");
        using (Assert.EnterMultipleScope()) {
            Assert.That(name, Has.Length.EqualTo(200));
            Assert.That(name, Does.EndWith(".pdf"));
        }
    }

    [Test]
    public void Unique_ReturnsTheSafeNameWhenItIsNotTaken() {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "photo.png" };
        Assert.That(MBoxAttachmentName.Unique("sub/report.pdf", taken), Is.EqualTo("report.pdf"));
    }

    [Test]
    public void Unique_NumbersANameThatIsTaken() {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "report.pdf", "REPORT (2).pdf" };
        Assert.That(MBoxAttachmentName.Unique("report.pdf", taken), Is.EqualTo("report (3).pdf"));
    }
}
