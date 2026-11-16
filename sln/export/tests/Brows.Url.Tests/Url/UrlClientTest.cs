using System;

namespace Brows.Url;

[TestFixture]
public sealed class UrlClientTest {
    [TestCase("ftp://user:secret@host/path/", "ftp://user:****@host/path/")]
    [TestCase("ftp://user:@host/", "ftp://user:****@host/")]
    [TestCase("ftp://user@host/path/", "ftp://user@host/path/")]
    [TestCase("ftp://host/path/", "ftp://host/path/")]
    public void Redact_ReplacesAPasswordWithAsterisks(string uri, string expected) {
        Assert.That(UrlClient.Redact(new Uri(uri)), Is.EqualTo(expected));
    }

    [Test]
    public void Redact_ReturnsNullForNull() {
        Assert.That(UrlClient.Redact(null), Is.Null);
    }
}
