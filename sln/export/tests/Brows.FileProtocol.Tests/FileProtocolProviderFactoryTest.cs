using Brows.Tests;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolProviderFactoryTest {
    private TestProviderFactory Subject;

    [TearDown]
    public void TearDown() {
        Subject?.Created?.Dispose();
        Subject = null;
    }

    [Test]
    public async Task CreateFor_ReturnsNullWhenIdIsNull() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID(null);
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_DoesNotDelegateWhenIdIsNull() {
        Subject = new TestProviderFactory("ftp");
        await Subject.CreateForID(null);
        Assert.That(Subject.CreateForCount, Is.Zero);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("sftp://host/path")]
    [TestCase("C:\\path")]
    [TestCase("/path")]
    [TestCase("not a uri at all")]
    public async Task CreateFor_ReturnsNullWhenIdDoesNotStartWithTheScheme(string id) {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID(id);
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_ReturnsNullWhenIdIsOnlyTheScheme() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("ftp");
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_ReturnsNullWhenTheSchemeIsOnlyAPrefixOfTheUriScheme() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("ftpes://host/path");
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_ReturnsNullWhenTheUriIsRelative() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("ftp:relative");
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_ReturnsAProviderForAMatchingId() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("ftp://host/path");
        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    public async Task CreateFor_DelegatesTheParsedUri() {
        Subject = new TestProviderFactory("ftp");
        await Subject.CreateForID("ftp://host/path/");
        Assert.That(Subject.CreateForUri, Is.EqualTo(new Uri("ftp://host/path/")));
    }

    [Test]
    public async Task CreateFor_DelegatesExactlyOnce() {
        Subject = new TestProviderFactory("ftp");
        await Subject.CreateForID("ftp://host/path/");
        Assert.That(Subject.CreateForCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateFor_ReturnsAProviderForTheRootOfTheHost() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("ftp://host");
        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    public async Task CreateFor_MatchesTheSchemeCaseInsensitively() {
        Subject = new TestProviderFactory("ftp");
        var provider = await Subject.CreateForID("FTP://host/path");
        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    public async Task CreateFor_MatchesWhenTheConfiguredSchemeIsUpperCase() {
        Subject = new TestProviderFactory("FTP");
        var provider = await Subject.CreateForID("ftp://host/path");
        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    public async Task CreateFor_ReturnsNullWhenTheDelegatedTaskIsNull() {
        Subject = new TestProviderFactory("ftp") { NullTask = true };
        var provider = await Subject.CreateForID("ftp://host/path");
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_ThrowsWhenTheTokenIsAlreadyCanceled() {
        Subject = new TestProviderFactory("ftp");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => Subject.CreateForID("ftp://host/path", cancel.Token));
    }

    [Test]
    public void Constructor_ThrowsWhenTheSchemeIsNull() {
        Assert.Throws<ArgumentNullException>(() => new TestProviderFactory(null));
    }
}
