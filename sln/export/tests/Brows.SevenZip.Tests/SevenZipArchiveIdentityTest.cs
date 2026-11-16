using Brows.Composition;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class SevenZipArchiveIdentityTest {
    private SevenZipTestSandbox Sandbox;

    private sealed class TestCaseCorrectFile : ICaseCorrectFile {
        private readonly string CorrectPath;

        public TestCaseCorrectFile(string correctPath) {
            CorrectPath = correctPath;
        }

        public Task<bool> Work(string file, Action<string> set, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            set(CorrectPath);
            return Task.FromResult(true);
        }
    }

    private sealed class TestCaseSensitiveDirectory : ICaseSensitiveDirectory {
        private readonly bool IsCaseSensitive;

        public TestCaseSensitiveDirectory(bool isCaseSensitive) {
            IsCaseSensitive = isCaseSensitive;
        }

        public Task<bool> Work(string directory, Action<bool> set, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            set(IsCaseSensitive);
            return Task.FromResult(true);
        }
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public async Task Create_UsesCaseSensitivityOfTheParentDirectoryForMissingPaths() {
        var lowerPath = Sandbox.PathFor("archive.7z");
        var upperPath = Sandbox.PathFor("ARCHIVE.7Z");
        var caseInsensitive = new TestCaseSensitiveDirectory(false);
        var insensitiveLower = await SevenZipArchiveIdentity.Create(
            new FileInfo(lowerPath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseInsensitive,
            token: CancellationToken.None);
        var insensitiveUpper = await SevenZipArchiveIdentity.Create(
            new FileInfo(upperPath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseInsensitive,
            token: CancellationToken.None);
        var caseSensitive = new TestCaseSensitiveDirectory(true);
        var sensitiveLower = await SevenZipArchiveIdentity.Create(
            new FileInfo(lowerPath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseSensitive,
            token: CancellationToken.None);
        var sensitiveUpper = await SevenZipArchiveIdentity.Create(
            new FileInfo(upperPath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseSensitive,
            token: CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(insensitiveUpper, Is.EqualTo(insensitiveLower));
            Assert.That(sensitiveUpper, Is.Not.EqualTo(sensitiveLower));
            Assert.That(sensitiveLower, Is.Not.EqualTo(insensitiveLower));
        }
    }

    [Test]
    public async Task Create_UsesCorrectedPathForAnExistingFile() {
        var filePath = Sandbox.WriteFile("archive.7z", "archive");
        var expectedPath = Sandbox.PathFor("stored.7z");
        var identity = await SevenZipArchiveIdentity.Create(
            new FileInfo(filePath),
            new TestCaseCorrectFile(expectedPath),
            new TestCaseSensitiveDirectory(false),
            CancellationToken.None);
        Assert.That(identity.FullName, Is.EqualTo(Path.GetFullPath(expectedPath)));
    }

    [TestCase("archive.7z")]
    [TestCase("missing/archive.7z")]
    public async Task Create_SkipsFileCaseCorrectionForANewArchive(string relativePath) {
        var filePath = Sandbox.PathFor(relativePath);
        var caseCorrectFile = new Mock<ICaseCorrectFile>();
        caseCorrectFile
            .Setup(service => service.Work(
                It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception(2));
        var identity = await SevenZipArchiveIdentity.Create(
            new FileInfo(filePath),
            caseCorrectFile.Object,
            new TestCaseSensitiveDirectory(false),
            CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(identity.FullName, Is.EqualTo(Path.GetFullPath(filePath)));
            Assert.That(identity.IsCaseSensitive, Is.False);
        }
        caseCorrectFile.Verify(service => service.Work(
            It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Create_PropagatesCaseCorrectionFailuresForAnExistingArchive() {
        var filePath = Sandbox.WriteFile("archive.7z", "archive");
        var failure = new Win32Exception(5);
        var caseCorrectFile = new Mock<ICaseCorrectFile>();
        caseCorrectFile
            .Setup(service => service.Work(
                It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var exception = await Assert.ThrowsAsync<Win32Exception>(() => SevenZipArchiveIdentity.Create(
            new FileInfo(filePath),
            caseCorrectFile.Object,
            new TestCaseSensitiveDirectory(false),
            CancellationToken.None));
        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public async Task Create_NormalizesDriveLetterCasingForCaseSensitivePaths() {
        if (Path.DirectorySeparatorChar != '\\') {
            Assert.Ignore("Drive-letter paths are only available on Windows.");
        }
        var filePath = Sandbox.WriteFile("archive.7z", "archive");
        var lowerDrivePath = char.ToLowerInvariant(filePath[0]) + filePath[1..];
        var caseSensitive = new TestCaseSensitiveDirectory(true);
        var upperIdentity = await SevenZipArchiveIdentity.Create(
            new FileInfo(filePath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseSensitive,
            token: CancellationToken.None);
        var lowerIdentity = await SevenZipArchiveIdentity.Create(
            new FileInfo(lowerDrivePath),
            caseCorrectFile: null,
            caseSensitiveDirectory: caseSensitive,
            token: CancellationToken.None);
        Assert.That(lowerIdentity, Is.EqualTo(upperIdentity));
    }

    [Test]
    public async Task GetAsync_SharesMissingCaseVariantsWithoutComposition() {
        var lowerPath = Sandbox.PathFor("missing.7z");
        var upperPath = Sandbox.PathFor("MISSING.7Z");
        var lower = await SevenZipArchivePath.GetAsync(new FileInfo(lowerPath), CancellationToken.None);
        var upper = await SevenZipArchivePath.GetAsync(new FileInfo(upperPath), CancellationToken.None);
        try {
            Assert.That(upper, Is.SameAs(lower));
        }
        finally {
            upper.Release();
            lower.Release();
        }
    }

    [Test]
    public async Task Create_SharesPathAndLockerAcrossMissingAndExistingStates() {
        var archivePath = Sandbox.PathFor("archive.7z");
        var file = new FileInfo(archivePath);
        var caseInsensitive = new TestCaseSensitiveDirectory(false);
        var missingIdentity = await SevenZipArchiveIdentity.Create(
            file,
            caseCorrectFile: null,
            caseSensitiveDirectory: caseInsensitive,
            token: CancellationToken.None);
        var missingPath = SevenZipArchivePath.Get(missingIdentity);
        var missingLocker = SevenZipArchiveLocker.Get(missingIdentity);
        File.WriteAllText(archivePath, "archive");
        var existingIdentity = await SevenZipArchiveIdentity.Create(
            file,
            caseCorrectFile: null,
            caseSensitiveDirectory: caseInsensitive,
            token: CancellationToken.None);
        var existingPath = SevenZipArchivePath.Get(existingIdentity);
        var existingLocker = SevenZipArchiveLocker.Get(existingIdentity);
        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(existingIdentity, Is.EqualTo(missingIdentity));
                Assert.That(existingPath, Is.SameAs(missingPath));
                Assert.That(existingLocker, Is.SameAs(missingLocker));
            }
        }
        finally {
            existingLocker.Release();
            missingLocker.Release();
            existingPath.Release();
            missingPath.Release();
        }
    }
}
