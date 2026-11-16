using Brows.Security;
using System;
using System.Threading.Tasks;

namespace Brows.Url;

[TestFixture]
public sealed class UrlClientCacheTest {
    private sealed class MockClient : UrlClient {
    }

    private sealed class MockClientCache : UrlClientCache<MockClient> {
    }

    private MockClientCache Subject {
        get => _Subject ??= new();
        set => _Subject = value;
    }
    private MockClientCache _Subject;

    [SetUp]
    public void SetUp() {
        Subject = null;
    }

    public static object[] Urls => new object[] {
        "http://hello.world",
        "https://goodbye.earth"
    };

    [TestCaseSource(nameof(Urls))]
    public void Get_ReturnsAnInstance(string url) {
        var obj = Subject.Get(new Uri(url), Array.Empty<MockClient>());
        Assert.That(obj, Is.Not.Null);
    }

    [TestCaseSource(nameof(Urls))]
    public void Get_ReturnsTheSameInstance(string url) {
        var obj1 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
        var obj2 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
        Assert.That(obj1, Is.SameAs(obj2));
    }

    [TestCaseSource(nameof(Urls))]
    public void Get_ReturnsAnotherInstance(string url) {
        var obj1 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
        var obj2 = Subject.Get(new Uri(url + "/more"), Array.Empty<MockClient>());
        Assert.That(obj1, Is.Not.SameAs(obj2));
    }

    [Test]
    public void Get_DistinguishesCaseSensitiveAndDifferentUserInfo() {
        var upperUri = new Uri("ftp://Alice:one@some.server/path");
        var lowerUri = new Uri("ftp://alice:one@some.server/path");
        var otherUri = new Uri("ftp://bob:one@some.server/path");
        var upper = Subject.Get(upperUri);
        upper.Credential.Username = "upper-profile";
        var lower = Subject.Get(lowerUri);
        var other = Subject.Get(otherUri);

        Assert.Multiple(() => {
            Assert.That(lower, Is.Not.SameAs(upper));
            Assert.That(other, Is.Not.SameAs(upper));
            Assert.That(other, Is.Not.SameAs(lower));
            Assert.That(upper.Uri.UserInfo, Is.EqualTo(upperUri.UserInfo));
            Assert.That(lower.Uri.UserInfo, Is.EqualTo(lowerUri.UserInfo));
            Assert.That(other.Uri.UserInfo, Is.EqualTo(otherUri.UserInfo));
            Assert.That(lower.Credential.Username, Is.Not.EqualTo("upper-profile"));
            Assert.That(other.Credential.Username, Is.Not.EqualTo("upper-profile"));
        });
    }

    [Test]
    public void Get_DistinguishesRawUserInfoPasswordsAndEncodedColons() {
        var first = Subject.Get(new Uri("ftp://alice:one@some.server/path"));
        var passwordVariant = Subject.Get(new Uri("ftp://alice:two@some.server/path"));
        var encodedColonVariant = Subject.Get(new Uri("ftp://alice%3Atwo@some.server/path"));

        Assert.Multiple(() => {
            Assert.That(passwordVariant, Is.Not.SameAs(first));
            Assert.That(encodedColonVariant, Is.Not.SameAs(passwordVariant));
            Assert.That(passwordVariant.Uri.UserInfo, Is.EqualTo("alice:two"));
            Assert.That(encodedColonVariant.Uri.UserInfo, Is.EqualTo("alice%3Atwo"));
        });
    }

    [Test]
    public void Get_DoesNotCopyDifferentUserInfoFromConfiguredClients() {
        var configured = new MockClient { Uri = new Uri("ftp://Alice:one@some.server/path") };
        configured.Credential.Username = "upper-profile";
        var requested = Subject.Get(new Uri("ftp://alice:one@some.server/path"), [configured]);

        Assert.Multiple(() => {
            Assert.That(requested, Is.Not.SameAs(configured));
            Assert.That(requested.Uri.UserInfo, Is.EqualTo("alice:one"));
            Assert.That(requested.Credential.Username, Is.Not.EqualTo("upper-profile"));
        });
    }

    public static object[] Extends = [
        new object[] { "ftp://some.server/path1", "ftp://some.server/path2" },
        new object[] { "ftp://some.server/path1", "ftp://some.server/path1/path2" }
    ];

    [TestCaseSource(nameof(Extends))]
    public void Get_UriIsCorrectAfterExtendingAnotherInstance(string url1, string url2) {
        var obj1 = Subject.Get(new Uri(url1));
        var obj2 = Subject.Get(new Uri(url2));
        Assert.That(obj2.Uri, Is.EqualTo(new Uri(url2)));
    }

    [TestCaseSource(nameof(Extends))]
    public void Get_CopiesUsernameWhileExtending(string url1, string url2) {
        var obj1 = Subject.Get(new Uri(url1));
        obj1.Credential.Username = "cool_dude1";
        var obj2 = Subject.Get(new Uri(url2));
        Assert.That(obj2.Credential.Username, Is.EqualTo("cool_dude1"));
    }

    [Test]
    public void Get_CopiesProfileOnlyWhenExtendingTheSameRawUserInfo() {
        var parent = Subject.Get(new Uri("ftp://Alice:one@some.server/path"));
        parent.Credential.Username = "upper-profile";
        var child = Subject.Get(new Uri("ftp://Alice:one@some.server/path/child"));

        Assert.Multiple(() => {
            Assert.That(child, Is.Not.SameAs(parent));
            Assert.That(child.Uri.UserInfo, Is.EqualTo(parent.Uri.UserInfo));
            Assert.That(child.Credential.Username, Is.EqualTo("upper-profile"));
        });
    }

    [Test]
    public void Get_CopiesConfiguredProfileWhenExtendingTheSameRawUserInfo() {
        var configured = new MockClient { Uri = new Uri("ftp://Alice:one@some.server/path") };
        configured.Credential.Username = "upper-profile";
        var child = Subject.Get(new Uri("ftp://Alice:one@some.server/path/child"), [configured]);

        Assert.Multiple(() => {
            Assert.That(child, Is.Not.SameAs(configured));
            Assert.That(child.Uri.UserInfo, Is.EqualTo(configured.Uri.UserInfo));
            Assert.That(child.Credential.Username, Is.EqualTo("upper-profile"));
        });
    }

    [Test]
    public void Get_FallbackSharesProfileOnlyForTheSameRawUserInfoAuthority() {
        var parent = Subject.Get(new Uri("ftp://Alice:one@some.server/first"));
        parent.Credential.Username = "upper-profile";
        var sibling = Subject.Get(new Uri("ftp://Alice:one@some.server/second"));
        var otherAccount = Subject.Get(new Uri("ftp://alice:one@some.server/third"));

        Assert.Multiple(() => {
            Assert.That(sibling.Credential.Username, Is.EqualTo("upper-profile"));
            Assert.That(otherAccount, Is.Not.SameAs(parent));
            Assert.That(otherAccount.Credential.Username, Is.Not.EqualTo("upper-profile"));
        });
    }

    [TestCaseSource(nameof(Extends))]
    public async Task Get_CopiesPasswordWhileExtending(string url1, string url2) {
        var obj1 = Subject.Get(new Uri(url1));
        await obj1.Credential.ProtectPassword("p@ssword1", SecurityDataKind.PlainText, default);
        var obj2 = Subject.Get(new Uri(url2));
        Assert.That(obj2.Credential.Password, Is.EqualTo("p@ssword1"));
    }
}
