using Brows.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FtpClientTest {
    [TestCase("ftp://example.test/", "Auto", "None")]
    [TestCase("ftps://example.test/", "Auto", "Implicit")]
    [TestCase("ftps://example.test/", "None", "Implicit")]
    [TestCase("ftp://example.test/", "Explicit", "Explicit")]
    [TestCase("ftps://example.test/", "Explicit", "Explicit")]
    [TestCase("ftpes://example.test/?brows-tls=explicit", "None", "Explicit")]
    public void GetTlsMode_UsesConfiguredSchemeAndMode(string uri, string configured, string expected) {
        var actual = FtpClient.GetTlsMode(new Uri(uri), Enum.Parse<FtpTlsMode>(configured));
        Assert.That(actual, Is.EqualTo(Enum.Parse<FtpTlsMode>(expected)));
    }

    [TestCase("ftps://localhost/", "ftps://localhost/")]
    [TestCase("ftpes://localhost/?brows-tls=explicit", "ftp://localhost/")]
    public async Task ResolveUri_PreservesTlsHostnameForTheNativeUrlByDefault(string uri, string expected) {
        var client = new FtpClient { Uri = new Uri(uri) };
        var nativeUri = client.GetTransferUri(await client.ResolveUri(client.Uri, CancellationToken.None));

        Assert.Multiple(() => {
            Assert.That(client.Resolve, Is.True);
            Assert.That(nativeUri.AbsoluteUri, Is.EqualTo(expected));
        });
    }

    [TestCase("ftps://localhost/")]
    [TestCase("ftpes://localhost/?brows-tls=explicit")]
    public async Task ResolveUri_PreservesTlsHostnameWhenResolutionIsRequested(string uri) {
        var client = new FtpClient {
            Uri = new Uri(uri),
            Resolve = true
        };
        var nativeUri = client.GetTransferUri(await client.ResolveUri(client.Uri, CancellationToken.None));

        Assert.That(nativeUri.Host, Is.EqualTo("localhost"));
    }

    [Test]
    public void ExplicitTlsUri_StripsMarkerFromNativeUrisAcrossNavigation() {
        var uri = Ftp.ExplicitUri(new Uri("ftp://example.test/root/"));
        var childUri = FileProtocolUri.Extend(uri, ["nested"], includeTrailingSlash: true);
        var cache = new FtpClientCache();
        var parent = cache.Get(uri);
        parent.Credential.Username = "ftp-test";
        var child = cache.Get(childUri);
        var transferUri = child.GetTransferUri(child.Uri);
        var commandUri = child.CommandUri;
        var uploadUri = child.UploadUri("/root/nested/space name.txt");
        var serverAbsoluteUploadUri = child.UploadUri("//root/nested/space name.txt");
        var serverAbsoluteUploadTransferUri = child.GetTransferUri(serverAbsoluteUploadUri);
        var unusualUploadUri = child.GetTransferUri(child.UploadUri("/root/nested/colon:name.txt"));
        var commandTransferUri = child.GetTransferUri(commandUri);
        var uploadTransferUri = child.GetTransferUri(uploadUri);
        Assert.That(parent.EffectiveTlsMode, Is.EqualTo(FtpTlsMode.Explicit));
        Assert.That(child.EffectiveTlsMode, Is.EqualTo(FtpTlsMode.Explicit));
        Assert.That(child.Credential.Username, Is.EqualTo("ftp-test"));
        Assert.That(uri.Scheme, Is.EqualTo("ftpes"));
        Assert.That(childUri.Query, Is.EqualTo("?brows-tls=explicit"));
        Assert.That(parent.GetTransferUri(uri).AbsoluteUri, Is.EqualTo("ftp://example.test/root/"));
        Assert.That(transferUri.Scheme, Is.EqualTo("ftp"));
        Assert.That(transferUri.Query, Is.Empty);
        Assert.That(transferUri.AbsoluteUri, Is.EqualTo("ftp://example.test/root/nested/"));
        Assert.That(commandTransferUri.AbsoluteUri, Is.EqualTo("ftp://example.test/"));
        Assert.That(uploadTransferUri.AbsoluteUri, Is.EqualTo("ftp://example.test/root/nested/space%20name.txt"));
        Assert.That(
            serverAbsoluteUploadTransferUri.AbsoluteUri,
            Is.EqualTo("ftp://example.test//root/nested/space%20name.txt"));
        Assert.That(unusualUploadUri.AbsoluteUri, Is.EqualTo("ftp://example.test/root/nested/colon%3Aname.txt"));
        Assert.That(commandTransferUri.Query, Is.Empty);
        Assert.That(uploadTransferUri.Query, Is.Empty);
    }

    [Test]
    public void ExplicitTlsUri_RequiresFtpScheme() {
        Assert.That(
            () => Ftp.ExplicitUri(new Uri("ftps://example.test/")),
            Throws.ArgumentException);
    }

    [Test]
    public void Cache_DistinguishesCaseSensitiveAccountsAndTheirCredentials() {
        var cache = new FtpClientCache();
        var upperUri = new Uri("ftp://Alice:one@example.test/root/");
        var lowerUri = new Uri("ftp://alice:one@example.test/root/");
        var otherUri = new Uri("ftp://bob:one@example.test/root/");
        var upper = cache.Get(upperUri);
        upper.Credential.Username = "upper-profile";
        var lower = cache.Get(lowerUri);
        var other = cache.Get(otherUri);

        Assert.Multiple(() => {
            Assert.That(lower, Is.Not.SameAs(upper));
            Assert.That(other, Is.Not.SameAs(upper));
            Assert.That(other, Is.Not.SameAs(lower));
            Assert.That(upper.Uri.UserInfo, Is.EqualTo(upperUri.UserInfo));
            Assert.That(lower.Uri.UserInfo, Is.EqualTo(lowerUri.UserInfo));
            Assert.That(other.Uri.UserInfo, Is.EqualTo(otherUri.UserInfo));
            Assert.That(lower.Credential.Username, Is.Not.EqualTo("upper-profile"));
            Assert.That(other.Credential.Username, Is.Not.EqualTo("upper-profile"));
            Assert.That(ReferenceEquals(cache.Get(upperUri), upper), Is.True);
            Assert.That(ReferenceEquals(cache.Get(lowerUri), lower), Is.True);
            Assert.That(ReferenceEquals(cache.Get(otherUri), other), Is.True);
        });
    }

    [Test]
    public void Cache_ExtendsSameAccountWithProfileAndFtpOptions() {
        var cache = new FtpClientCache();
        var parentUri = new Uri("ftp://Alice:one@example.test/root/");
        var childUri = new Uri("ftp://Alice:one@example.test/root/child/");
        var parent = cache.Get(parentUri);
        parent.Credential.Username = "upper-profile";
        parent.TlsMode = FtpTlsMode.Explicit;
        parent.CertificateAuthorityFile = "trusted-ca.pem";
        parent.Resolve = false;
        var child = cache.Get(childUri);

        Assert.Multiple(() => {
            Assert.That(child, Is.Not.SameAs(parent));
            Assert.That(child.Uri.UserInfo, Is.EqualTo(parent.Uri.UserInfo));
            Assert.That(child.Credential.Username, Is.EqualTo("upper-profile"));
            Assert.That(child.TlsMode, Is.EqualTo(FtpTlsMode.Explicit));
            Assert.That(child.CertificateAuthorityFile, Is.EqualTo("trusted-ca.pem"));
            Assert.That(child.Resolve, Is.False);
        });
    }

    [Test]
    public void ResourceIdentity_UsesAnonymousForAnOmittedUsernameAndDropsPasswords() {
        var omittedUri = new Uri("ftp://example.test/root/");
        var explicitUri = new Uri("ftp://anonymous:uri-secret@example.test/root/");
        var omittedProvider = new TestProvider(omittedUri);
        var explicitProvider = new TestProvider(explicitUri);

        var omittedIdentity = omittedProvider.ResourceIdentity(omittedUri);
        var explicitIdentity = explicitProvider.ResourceIdentity(explicitUri);

        Assert.Multiple(() => {
            Assert.That(omittedIdentity.AbsoluteUri, Is.EqualTo(explicitIdentity.AbsoluteUri));
            Assert.That(omittedIdentity.UserInfo, Is.EqualTo("anonymous"));
            Assert.That(explicitIdentity.UserInfo, Is.EqualTo("anonymous"));
            Assert.That(omittedIdentity.AbsoluteUri, Does.Not.Contain("uri-secret"));
            Assert.That(explicitIdentity.AbsoluteUri, Does.Not.Contain("uri-secret"));
        });
    }

    [Test]
    public void ResourceIdentity_UsesConfiguredUsernameInsteadOfUriUsername() {
        var sourceUri = new Uri("ftp://alice:source-secret@example.test/root/");
        var targetUri = new Uri("ftp://bob:target-secret@example.test/root/");
        var sourceProvider = new TestProvider(sourceUri);
        var targetProvider = new TestProvider(targetUri);
        sourceProvider.ClientAt(sourceUri).Credential.Username = "bob";

        var sourceIdentity = sourceProvider.ResourceIdentity(sourceUri);
        var targetIdentity = targetProvider.ResourceIdentity(targetUri);

        Assert.Multiple(() => {
            Assert.That(sourceIdentity.AbsoluteUri, Is.EqualTo(targetIdentity.AbsoluteUri));
            Assert.That(sourceIdentity.UserInfo, Is.EqualTo("bob"));
            Assert.That(sourceIdentity.AbsoluteUri, Does.Not.Contain("source-secret"));
            Assert.That(sourceIdentity.AbsoluteUri, Does.Not.Contain("target-secret"));
        });
    }

    [Test]
    public void ResourceIdentity_EscapesConfiguredUsernameWithoutSplittingColonOrUnicode() {
        var uri = new Uri("ftp://old-user:uri-secret@example.test/root/");
        var provider = new TestProvider(uri);
        provider.ClientAt(uri).Credential.Username = "雪:猫";

        var identity = provider.ResourceIdentity(uri);

        Assert.Multiple(() => {
            Assert.That(Uri.UnescapeDataString(identity.UserInfo), Is.EqualTo("雪:猫"));
            Assert.That(identity.UserInfo, Does.Contain("%3A"));
            Assert.That(identity.AbsoluteUri, Does.Not.Contain("uri-secret"));
        });
    }

    [Test]
    public void ResourceIdentity_DecodesAndReEscapesEncodedUriUsernames() {
        var uri = new Uri("ftp://%E9%9B%AA%3A%E7%8C%AB:uri-secret@example.test/root/");
        var provider = new TestProvider(uri);

        var identity = provider.ResourceIdentity(uri);

        Assert.Multiple(() => {
            Assert.That(Uri.UnescapeDataString(identity.UserInfo), Is.EqualTo("雪:猫"));
            Assert.That(identity.UserInfo, Does.Contain("%3A"));
            Assert.That(identity.AbsoluteUri, Does.Not.Contain("uri-secret"));
        });
    }

    [TestCase("ftp://example.test/root/", "Auto", "ftp://anonymous@example.test/root/")]
    [TestCase("ftpes://example.test/root/?brows-tls=explicit", "Auto", "ftp://anonymous@example.test/root/")]
    [TestCase("ftps://example.test/root/", "Auto", "ftps://anonymous@example.test/root/")]
    [TestCase("ftps://example.test/root/", "Explicit", "ftp://anonymous@example.test/root/")]
    [TestCase("ftp://example.test/root/", "Implicit", "ftps://anonymous@example.test:990/root/")]
    public void ResourceIdentity_UsesEffectiveTransferSchemeAndPort(
        string uri,
        string tlsMode,
        string expected) {
        var providerUri = new Uri(uri);
        var provider = new TestProvider(providerUri);
        provider.ClientAt(providerUri).TlsMode = Enum.Parse<FtpTlsMode>(tlsMode);

        var identity = provider.ResourceIdentity(providerUri);

        Assert.That(identity.AbsoluteUri, Is.EqualTo(expected));
    }

    [Test]
    public void ResourceIdentity_ConfiguredImplicitFtpMatchesImplicitFtpsUri() {
        var ftpUri = new Uri("ftp://example.test/root/");
        var ftpsUri = new Uri("ftps://example.test:990/root/");
        var ftpProvider = new TestProvider(ftpUri);
        var ftpsProvider = new TestProvider(ftpsUri);
        ftpProvider.ClientAt(ftpUri).TlsMode = FtpTlsMode.Implicit;

        var configuredImplicitIdentity = ftpProvider.ResourceIdentity(ftpUri);
        var ftpsIdentity = ftpsProvider.ResourceIdentity(ftpsUri);

        Assert.That(configuredImplicitIdentity.AbsoluteUri, Is.EqualTo(ftpsIdentity.AbsoluteUri));
    }

    [Test]
    public void ResourceIdentity_PreservesDifferentAccounts() {
        var aliceUri = new Uri("ftp://Alice@example.test/root/");
        var bobUri = new Uri("ftp://bob@example.test/root/");
        using var aliceProvider = new TestProvider(aliceUri);
        using var bobProvider = new TestProvider(bobUri);
        var alice = aliceProvider.ResourceIdentity(aliceUri);
        var bob = bobProvider.ResourceIdentity(bobUri);

        Assert.Multiple(() => {
            Assert.That(alice.UserInfo, Is.EqualTo("Alice"));
            Assert.That(bob.UserInfo, Is.EqualTo("bob"));
            Assert.That(alice.AbsoluteUri, Is.Not.EqualTo(bob.AbsoluteUri));
        });
    }

    private sealed class TestProvider : FtpProviderBase {
        public TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }

        private sealed class TestFactory : IFtpProviderFactory {
            public FtpClientCache Client { get; } = new();
        }
    }
}
