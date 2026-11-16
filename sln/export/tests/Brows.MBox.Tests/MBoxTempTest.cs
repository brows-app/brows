using System;
using System.IO;

namespace Brows;

[TestFixture]
internal sealed class MBoxTempTest {
    private MBoxTestDirectory _Directory;
    private string _Root;
    private string _SessionId;

    [SetUp]
    public void SetUp() {
        _Directory = new MBoxTestDirectory();
        _Root = _Directory.Directory.CreateSubdirectory("temporary").FullName;
        _SessionId = Guid.NewGuid().ToString("N");
    }

    [TearDown]
    public void TearDown() {
        MBoxTemp.Shutdown(_Root);
        _Directory.Dispose();
    }

    [Test]
    public void IsUnder_ReturnsTrueForAPathUnderTheRoot() {
        var path = Path.Combine(_Root, "session", "message-0.eml");
        Assert.That(MBoxTemp.IsUnder(_Root, path), Is.True);
    }

    [Test]
    public void IsUnder_ReturnsFalseForTheRootItself() {
        Assert.That(MBoxTemp.IsUnder(_Root, _Root), Is.False);
    }

    [Test]
    public void IsUnder_ReturnsFalseForAPathOutsideTheRoot() {
        var path = Path.Combine(_Directory.Directory.FullName, "other", "session");
        Assert.That(MBoxTemp.IsUnder(_Root, path), Is.False);
    }

    [Test]
    public void IsUnder_ReturnsFalseForAPathThatLeavesTheRoot() {
        var path = Path.Combine(_Root, "..", "outside");
        Assert.That(MBoxTemp.IsUnder(_Root, path), Is.False);
    }

    [Test]
    public void IsUnder_IgnoresTheCaseOfThePath() {
        var path = Path.Combine(_Root.ToUpperInvariant(), "session");
        Assert.That(MBoxTemp.IsUnder(_Root, path), Is.True);
    }

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("   ", "   ")]
    public void IsUnder_ReturnsFalseForAPathThatNamesNothing(string root, string path) {
        Assert.That(MBoxTemp.IsUnder(root, path), Is.False);
    }

    [Test]
    public void SessionDirectory_CreatesTheDirectoryOfTheSessionUnderTheInjectedRoot() {
        var directory = MBoxTemp.SessionDirectory(_SessionId, _Root);
        using (Assert.EnterMultipleScope()) {
            Assert.That(directory, Is.EqualTo(Path.Combine(_Root, _SessionId)));
            Assert.That(Directory.Exists(directory), Is.True);
        }
    }

    [Test]
    public void SessionDirectory_ThrowsWhenTheSessionLiesOutsideTheRoot() {
        Assert.Throws<InvalidOperationException>(() => MBoxTemp.SessionDirectory("..\\..\\outside", _Root));
    }

    [Test]
    public void Acquire_CreatesALiveOwnerLeaseAndShutdownRemovesItsFiles() {
        var directory = MBoxTemp.SessionDirectory(_SessionId, _Root);
        var message = Path.Combine(directory, MBoxPath.MessageName(0));
        MBoxTemp.Acquire(_SessionId, _Root);
        File.WriteAllText(message, "viewer contents");
        MBoxTemp.Shutdown(_Root);
        using (Assert.EnterMultipleScope()) {
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(File.Exists(message), Is.False);
        }
    }

    [Test]
    public void Clean_LeavesALiveOwnerButRemovesAStaleCrashMarkerAndOldUnownedDirectory() {
        var now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        var live = MBoxTemp.SessionDirectory(_SessionId, _Root);
        MBoxTemp.Acquire(_SessionId, _Root);
        Directory.SetLastWriteTimeUtc(live, now.AddDays(-MBoxTemp.RetentionDays - 1));
        var stale = CreateMarkedDirectory("stale", "crashed-session", now.AddDays(-MBoxTemp.RetentionDays - 1));
        var unowned = Directory.CreateDirectory(Path.Combine(_Root, "unowned"));
        Directory.SetLastWriteTimeUtc(unowned.FullName, now.AddDays(-MBoxTemp.RetentionDays - 1));
        var recent = Directory.CreateDirectory(Path.Combine(_Root, "recent"));
        Directory.SetLastWriteTimeUtc(recent.FullName, now.AddDays(-1));

        MBoxTemp.Clean(_Root, now);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Directory.Exists(live), Is.True);
            Assert.That(Directory.Exists(stale), Is.False);
            Assert.That(Directory.Exists(unowned.FullName), Is.False);
            Assert.That(Directory.Exists(recent.FullName), Is.True);
        }
    }

    [Test]
    public void Shutdown_LeavesALockedViewerFileForLaterCleanup() {
        var directory = MBoxTemp.SessionDirectory(_SessionId, _Root);
        MBoxTemp.Acquire(_SessionId, _Root);
        var message = Path.Combine(directory, MBoxPath.MessageName(0));
        File.WriteAllText(message, "viewer contents");
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddDays(-MBoxTemp.RetentionDays - 1));
        using (File.Open(message, FileMode.Open, FileAccess.Read, FileShare.None)) {
            MBoxTemp.Shutdown(_Root);
            Assert.That(File.Exists(message), Is.True);
        }
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddDays(-MBoxTemp.RetentionDays - 1));
        MBoxTemp.Clean(_Root, DateTime.UtcNow);
        Assert.That(Directory.Exists(directory), Is.False);
    }

    private string CreateMarkedDirectory(string name, string sessionId, DateTime lastWriteUtc) {
        var directory = Directory.CreateDirectory(Path.Combine(_Root, name));
        File.WriteAllText(Path.Combine(directory.FullName, MBoxTemp.OwnerFileName), sessionId);
        Directory.SetLastWriteTimeUtc(directory.FullName, lastWriteUtc);
        return directory.FullName;
    }
}
