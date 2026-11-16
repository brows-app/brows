using System;
using System.IO;

namespace Brows;

[TestFixture]
internal sealed class SevenZipArchiveLockerTest {
    private SevenZipTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public void Get_ReturnsTheSameLockerForCaseVariantPathsToTheSameFile() {
        var archivePath = Sandbox.WriteFile("archive.7z", "archive");
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var first = SevenZipArchiveLocker.GetForTest(new FileInfo(archivePath));
        var second = SevenZipArchiveLocker.GetForTest(new FileInfo(caseVariant));
        try {
            Assert.That(second, Is.SameAs(first));
        }
        finally {
            second.Release();
            first.Release();
        }
    }

    [Test]
    public void Get_ReturnsDistinctLockersForCaseVariantFilesInCaseSensitiveDirectory() {
        var directory = Sandbox.CreateDirectory("case-sensitive");
        if (SevenZipCaseSensitiveDirectory.TryEnable(directory) == false) {
            Assert.Ignore("The file system does not support case-sensitive directories here.");
        }
        var lowerPath = Path.Combine(directory, "archive.7z");
        var upperPath = Path.Combine(directory, "ARCHIVE.7Z");
        File.WriteAllText(lowerPath, "lower");
        File.WriteAllText(upperPath, "upper");
        var lower = SevenZipArchiveLocker.GetForTest(new FileInfo(lowerPath));
        var upper = SevenZipArchiveLocker.GetForTest(new FileInfo(upperPath));
        try {
            Assert.That(upper, Is.Not.SameAs(lower));
        }
        finally {
            upper.Release();
            lower.Release();
        }
    }
}
