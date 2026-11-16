using System;
using System.IO;
using System.Linq;

namespace Brows;

[TestFixture]
internal sealed class TarEntryNameTest {
    [Test]
    public void Parts_IgnoresBothSeparatorsAndDuplicates() {
        var name = new TarEntryName("/folder//sub\\file.txt");
        Assert.That(name.Parts, Is.EqualTo(new[] { "folder", "sub", "file.txt" }));
    }

    [Test]
    public void Normalized_UsesTheDirectorySeparator() {
        var name = new TarEntryName("folder/inner/file.txt");
        Assert.That(name.Normalized, Is.EqualTo(Path.Combine("folder", "inner", "file.txt")));
    }

    [Test]
    public void Top_IsTheNameOfTheLastPart() {
        var name = new TarEntryName("folder/inner/file.txt");
        Assert.That(name.Top, Is.EqualTo("file.txt"));
    }

    [Test]
    public void Parent_IsEmptyForATopLevelEntry() {
        var name = new TarEntryName("file.txt");
        Assert.That(name.Parent, Is.Empty);
    }

    [Test]
    public void Parent_IsTheFoldersAboveTheEntry() {
        var name = new TarEntryName("folder/inner/file.txt");
        Assert.That(name.Parent, Is.EqualTo(Path.Combine("folder", "inner")));
    }

    [Test]
    public void Paths_AreTheFoldersAboveTheEntry() {
        var name = new TarEntryName("folder/inner/file.txt");
        Assert.That(name.Paths.Select(path => path), Is.EquivalentTo(new[] {
            Path.Combine("folder"),
            Path.Combine("folder", "inner")
        }));
    }

    [Test]
    public void Paths_AreEmptyForATopLevelEntry() {
        var name = new TarEntryName("file.txt");
        Assert.That(name.Paths, Is.Empty);
    }

    [Test]
    public void Original_IsTheNameThatTheArchiveStores() {
        var name = new TarEntryName("folder/trailing/");
        Assert.That(name.Original, Is.EqualTo("folder/trailing/"));
    }
}
