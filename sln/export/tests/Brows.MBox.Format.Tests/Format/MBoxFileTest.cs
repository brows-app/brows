using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the mailbox file, its leases, and the way it shares its handle.
/// </summary>
[TestFixture]
public sealed class MBoxFileTest {
    private string _Path;

    [SetUp]
    public void SetUp() {
        _Path = Path.Combine(Path.GetTempPath(), $"brows-mbox-{Guid.NewGuid():N}.mbox");
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_Path)) {
            File.Delete(_Path);
        }
    }

    [Test]
    public void File_ReadsTheRecordsOfTheMailbox() {
        Write(MBoxFixture.Mailbox(["First.\n", "Second.\n"]));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        Assert.That(records.Count, Is.EqualTo(2));
    }

    [Test]
    public void File_ReportsTheCanonicalPathAndTheLength() {
        var text = MBoxFixture.Mailbox("The body.\n");
        Write(text);
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        Assert.That(file.Path, Is.EqualTo(Path.GetFullPath(_Path)));
        Assert.That(file.Length, Is.EqualTo((long)text.Length));
    }

    [Test]
    public void File_ReadsTheMessageOfARecord() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        var message = file.ReadMessage(records[0], CancellationToken.None);
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void File_ExtractsARecordAsAnInternetMessage() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        using var destination = new MemoryStream();
        file.ExtractMessage(records[0], destination, CancellationToken.None);
        Assert.That(Encoding.UTF8.GetString(destination.ToArray()), Is.EqualTo("\nThe body.\n"));
    }

    [Test]
    public void File_ReportsAMailboxThatDoesNotExist() {
        var missing = Path.Combine(Path.GetDirectoryName(_Path), $"missing-{Guid.NewGuid():N}.mbox");
        Assert.That(
            () => new MBoxFile(missing, MBoxParserOptions.Default),
            Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public void File_ReportsAMailboxThatIsLockedForWriting() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var exclusive = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(
            () => new MBoxFile(_Path, MBoxParserOptions.Default),
            Throws.TypeOf<IOException>());
    }

    [Test]
    public void File_ReportsAMailboxThatIsLockedForDeletion() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var exclusive = new FileStream(_Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.That(
            () => File.Delete(_Path),
            Throws.TypeOf<IOException>());
    }

    [Test]
    public void File_KeepsTheMailboxReadableWhileItIsOpen() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        using var other = new FileStream(_Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.That(other.Length, Is.EqualTo(file.Length));
    }

    [Test]
    public void File_DoesNotShareThePositionOfTwoReaders() {
        Write(MBoxFixture.Mailbox(["First.\n", "Second.\n"]));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        var first = file.ReadMessage(records[0], CancellationToken.None);
        var second = file.ReadMessage(records[1], CancellationToken.None);
        Assert.That(first.Body.Text, Is.EqualTo("First.\n"));
        Assert.That(second.Body.Text, Is.EqualTo("Second.\n"));
    }

    [Test]
    public void File_RejectsANewOperationAfterItIsDisposed() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        file.Dispose();
        Assert.That(
            () => file.ReadMessage(records[0], CancellationToken.None),
            Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void File_ReleasesItsHandleWhenItIsDisposed() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        file.Dispose();
        using var exclusive = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(exclusive.Length, Is.GreaterThan(0));
    }

    [Test]
    public void File_KeepsTheMailboxLockedWhileALeaseIsRunning() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var scan = file.Scan(null, CancellationToken.None).GetEnumerator();
        scan.MoveNext();
        Assert.That(
            () => {
                using var exclusive = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            },
            Throws.TypeOf<IOException>());
        scan.Dispose();
    }

    [Test]
    public void File_ReportsAMailboxThatIsEmpty() {
        Write(string.Empty);
        using var file = new MBoxFile(_Path, MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(file.Scan(null, CancellationToken.None));
        Assert.That(records, Is.Empty);
    }

    [Test]
    public void File_KeepsTheDataOfTheMailboxAfterItIsDisposed() {
        Write(MBoxFixture.Mailbox("The body.\n"));
        var records = new List<MBoxRecord>();
        using (var file = new MBoxFile(_Path, MBoxParserOptions.Default)) {
            records.AddRange(file.Scan(null, CancellationToken.None));
        }
        using var other = new MBoxFile(_Path, MBoxParserOptions.Default);
        var message = other.ReadMessage(records[0], CancellationToken.None);
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    private void Write(string text) {
        File.WriteAllText(_Path, text, new UTF8Encoding(false));
    }
}
