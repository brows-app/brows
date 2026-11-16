using System;
using System.IO;

namespace Brows;

[TestFixture]
internal sealed class SevenZipEntryNameTest {
    [Test]
    public void Original_IsTheNameThatWasGiven() {
        var name = new SevenZipEntryName("folder/file.txt");
        Assert.That(name.Original, Is.EqualTo("folder/file.txt"));
    }

    [Test]
    public void Parts_SplitsTheNameAtTheSeparators() {
        var name = new SevenZipEntryName("folder/sub/file.txt");
        Assert.That(name.Parts, Is.EqualTo(new[] { "folder", "sub", "file.txt" }));
    }

    [Test]
    public void Parts_SplitsTheNameAtBackslashes() {
        var name = new SevenZipEntryName("folder\\file.txt");
        Assert.That(name.Parts, Is.EqualTo(new[] { "folder", "file.txt" }));
    }

    [Test]
    public void Parts_IgnoresEmptyParts() {
        var name = new SevenZipEntryName("/folder//file.txt");
        Assert.That(name.Parts, Is.EqualTo(new[] { "folder", "file.txt" }));
    }

    [Test]
    public void Normalized_UsesTheSeparatorOfTheSystem() {
        var name = new SevenZipEntryName("folder/file.txt");
        Assert.That(name.Normalized, Is.EqualTo($"folder{Path.DirectorySeparatorChar}file.txt"));
    }

    [Test]
    public void Top_IsTheLastPartOfTheName() {
        var name = new SevenZipEntryName("folder/file.txt");
        Assert.That(name.Top, Is.EqualTo("file.txt"));
    }

    [Test]
    public void Top_IsEmptyForAnEmptyName() {
        var name = new SevenZipEntryName("");
        Assert.That(name.Top, Is.EqualTo(""));
    }

    [Test]
    public void Parent_IsTheNameOfTheFolderThatHoldsTheItem() {
        var name = new SevenZipEntryName("folder/sub/file.txt");
        Assert.That(name.Parent, Is.EqualTo($"folder{Path.DirectorySeparatorChar}sub"));
    }

    [Test]
    public void Parent_IsEmptyForAnItemThatIsNotInAFolder() {
        var name = new SevenZipEntryName("file.txt");
        Assert.That(name.Parent, Is.EqualTo(""));
    }

    [Test]
    public void Paths_ListsTheFoldersThatHoldTheItem() {
        var name = new SevenZipEntryName("folder/sub/file.txt");
        Assert.That(name.Paths, Is.EqualTo(new[] {
            "folder",
            $"folder{Path.DirectorySeparatorChar}sub"
        }));
    }

    [Test]
    public void Paths_IsEmptyForAnItemThatIsNotInAFolder() {
        var name = new SevenZipEntryName("file.txt");
        Assert.That(name.Paths, Is.Empty);
    }

    [Test]
    public void Constructor_ThrowsWithoutAName() {
        Assert.That(() => new SevenZipEntryName(null), Throws.InstanceOf<ArgumentNullException>());
    }
}
