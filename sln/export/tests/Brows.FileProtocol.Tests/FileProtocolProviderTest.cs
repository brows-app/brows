using Brows.Tests;
using System;

namespace Brows;

[TestFixture]
public sealed class FileProtocolProviderTest {
    private TestProvider Subject;

    [TearDown]
    public void TearDown() {
        Subject?.Dispose();
        Subject = null;
    }

    [Test]
    public void Uri_IsTheConstructedUri() {
        Subject = new TestProvider("ftp://host/a/b/");
        Assert.That(Subject.Uri, Is.EqualTo(new Uri("ftp://host/a/b/")));
    }

    [Test]
    public void Constructor_ThrowsWhenUriIsNull() {
        Assert.Throws<ArgumentNullException>(() => new TestProvider((Uri)null));
    }

    [TestCase("ftp://host/a/b/", "ftp://host/a/")]
    [TestCase("ftp://host/a/b", "ftp://host/a/")]
    [TestCase("ftp://host/a/", "ftp://host/")]
    [TestCase("ftp://host/a", "ftp://host/")]
    public void Parent_IsTheContainingDirectory(string uri, string expected) {
        Subject = new TestProvider(uri);
        Assert.That(Subject.Parent, Is.EqualTo(expected));
    }

    [TestCase("ftp://host/")]
    [TestCase("ftp://host")]
    public void Parent_IsNullAtTheRoot(string uri) {
        Subject = new TestProvider(uri);
        Assert.That(Subject.Parent, Is.Null);
    }

    [Test]
    public void Parent_PreservesANonDefaultPort() {
        Subject = new TestProvider("ftp://host:2121/a/b/");
        Assert.That(Subject.Parent, Is.EqualTo("ftp://host:2121/a/"));
    }

    [Test]
    public void Parent_DoesNotAddTheDefaultPort() {
        Subject = new TestProvider("ftp://host/a/b/");
        Assert.That(Subject.Parent, Does.Not.Contain(":21/"));
    }

    [Test]
    public void Parent_IsAParsableUri() {
        Subject = new TestProvider("ftp://host/a/b/");
        Assert.That(Uri.TryCreate(Subject.Parent, UriKind.Absolute, out _), Is.True);
    }

    [Test]
    public void Parent_IsTheSameOnEachAccess() {
        Subject = new TestProvider("ftp://host/a/b/");
        var firstParent = Subject.Parent;
        Assert.That(Subject.Parent, Is.SameAs(firstParent));
    }

    [Test]
    public void Parent_IsStillNullOnTheSecondAccessAtTheRoot() {
        Subject = new TestProvider("ftp://host/");
        _ = Subject.Parent;
        Assert.That(Subject.Parent, Is.Null);
    }

    [Test]
    public void Parent_RoundTripsAnEscapedPath() {
        Subject = new TestProvider("ftp://host/a%20b/c/");
        Assert.That(new Uri(Subject.Parent), Is.EqualTo(new Uri("ftp://host/a%20b/")));
    }

    [Test]
    public void Parent_IsRenderedTheSameWayAsTheProviderID() {
        Subject = new TestProvider("ftp://host/a%20b/c/");
        Assert.That(Subject.Parent, Is.EqualTo(new Uri("ftp://host/a%20b/").ToString()));
    }

    [Test]
    public void ID_IsTheUriString() {
        Subject = new TestProvider("ftp://host/a/b/");
        Assert.That(((IProvider)Subject).ID, Is.EqualTo("ftp://host/a/b/"));
    }
}
