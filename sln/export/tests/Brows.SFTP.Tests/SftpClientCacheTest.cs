using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class SftpClientCacheTest {
    [Test]
    public void Acquire_CaseDistinctUsernamesUseSeparateClients() {
        var cache = new SftpClientCache();
        var knownHosts = KnownHostsPath();
        using var upper = cache.Acquire(new Uri("sftp://Alice@example.com:22/"), knownHosts);
        using var lower = cache.Acquire(new Uri("sftp://alice@example.com:22/"), knownHosts);

        Assert.That(lower.Client, Is.Not.SameAs(upper.Client));
        Assert.That(upper.Client.Username, Is.EqualTo("Alice"));
        Assert.That(lower.Client.Username, Is.EqualTo("alice"));
    }

    [Test]
    public void Acquire_CaseInsensitiveAuthorityAndIgnoredUrlComponentsReuseClient() {
        var cache = new SftpClientCache();
        var knownHosts = KnownHostsPath();
        using var first = cache.Acquire(new Uri("sftp://alice:one@EXAMPLE.com:22/first?x=1#first"), knownHosts);
        using var second = cache.Acquire(new Uri("SFTP://al%69ce:two@example.COM:22/second?x=2#second"), knownHosts);

        Assert.That(second.Client, Is.SameAs(first.Client));
    }

    [Test]
    public void Acquire_MissingUsernameAndExplicitCurrentUsernameReuseClient() {
        var cache = new SftpClientCache();
        var knownHosts = KnownHostsPath();
        var username = Uri.EscapeDataString(Environment.UserName);
        using var missing = cache.Acquire(new Uri("sftp://example.com:22/"), knownHosts);
        using var explicitUsername = cache.Acquire(new Uri($"sftp://{username}@example.com:22/"), knownHosts);

        Assert.That(explicitUsername.Client, Is.SameAs(missing.Client));
        Assert.That(missing.Client.Username, Is.EqualTo(Environment.UserName));
    }

    [Test]
    public void Acquire_OmittedAndExplicitDefaultPortReuseClient() {
        var cache = new SftpClientCache();
        var knownHosts = KnownHostsPath();
        using var omitted = cache.Acquire(new Uri("sftp://alice@example.com/"), knownHosts);
        using var explicitPort = cache.Acquire(new Uri("sftp://alice@example.com:22/"), knownHosts);

        Assert.That(explicitPort.Client, Is.SameAs(omitted.Client));
        Assert.That(omitted.Client.Port, Is.EqualTo(22));
    }

    [Test]
    public void Acquire_RejectsExplicitZeroPort() {
        var cache = new SftpClientCache();

        Assert.Throws<UriFormatException>(() =>
            cache.Acquire(new Uri("sftp://alice@example.com:0/"), KnownHostsPath()));
    }

    [Test]
    public void Acquire_DistinctKnownHostsFilesUseSeparateClientsWithOperatingSystemPathRules() {
        var cache = new SftpClientCache();
        var firstPath = KnownHostsPath();
        var secondPath = Path.Combine(Path.GetDirectoryName(firstPath), "other_known_hosts");
        using var first = cache.Acquire(new Uri("sftp://alice@example.com:22/"), firstPath);
        using var second = cache.Acquire(new Uri("sftp://alice@example.com:22/"), secondPath);
        using var caseVariant = cache.Acquire(
            new Uri("sftp://alice@example.com:22/"),
            firstPath.ToUpperInvariant());

        Assert.That(second.Client, Is.Not.SameAs(first.Client));
        Assert.That(ReferenceEquals(caseVariant.Client, first.Client), Is.EqualTo(OperatingSystem.IsWindows()));
    }

    [Test]
    public void Dispose_LeaseKeepsSharedClientUntilLastReferenceIsReleased() {
        var cache = new SftpClientCache();
        var uri = new Uri("sftp://alice@example.com:22/");
        var knownHosts = KnownHostsPath();
        var first = cache.Acquire(uri, knownHosts);
        var second = cache.Acquire(uri, knownHosts);
        var sharedClient = first.Client;

        Assert.That(second.Client, Is.SameAs(sharedClient));

        first.Dispose();
        using (var third = cache.Acquire(uri, knownHosts)) {
            Assert.That(third.Client, Is.SameAs(sharedClient));
            second.Dispose();
        }

        using var afterRelease = cache.Acquire(uri, knownHosts);
        Assert.That(afterRelease.Client, Is.Not.SameAs(sharedClient));
    }

    [Test]
    public void Acquire_ConcurrentRequestsShareOneClient() {
        var cache = new SftpClientCache();
        var uri = new Uri("sftp://alice@example.com:22/");
        var knownHosts = KnownHostsPath();
        var leases = new SftpClientLease[16];
        Parallel.For(0, leases.Length, index => {
            leases[index] = cache.Acquire(uri, knownHosts);
        });

        try {
            foreach (var lease in leases) {
                Assert.That(lease.Client, Is.SameAs(leases[0].Client));
            }
        }
        finally {
            foreach (var lease in leases) {
                lease.Dispose();
            }
        }
    }

    private static string KnownHostsPath() {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "known_hosts");
    }
}
