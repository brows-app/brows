using Brows.SFTP.Native;
using System;
using System.IO;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NativeSftpConnection = Brows.SFTP.Native.SftpConnection;

namespace Brows;

[TestFixture]
internal sealed class SftpClientTest {
    [Test]
    public async Task Constructor_ParsesIpv6AndEscapedUsername() {
        var uri = new Uri("sftp://user%20name@[2001:db8::5]:2222/root/");
        var client = new SftpClient(uri, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "known_hosts"));
        try {
            Assert.That(client.HostName, Is.EqualTo("2001:db8::5"));
            Assert.That(client.Port, Is.EqualTo(2222));
            Assert.That(client.Username, Is.EqualTo("user name"));
        }
        finally {
            await client.DisposeAsync();
        }
    }

    [Test]
    public async Task Constructor_UsesDefaultPortWhenSftpUriOmitsPort() {
        var uri = new Uri("sftp://user@example.com/root/");
        var client = new SftpClient(uri, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "known_hosts"));
        try {
            Assert.That(uri.Port, Is.EqualTo(-1));
            Assert.That(client.Port, Is.EqualTo(22));
        }
        finally {
            await client.DisposeAsync();
        }
    }

    [Test]
    public void Constructor_RejectsExplicitZeroPort() {
        var uri = new Uri("sftp://user@example.com:0/");

        Assert.Throws<UriFormatException>(() => new SftpClient(uri));
    }

    [Test]
    public void RemotePath_PreservesPosixNameBytesAndSegments() {
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@[::1]:2222/root/");
        using var lease = factory.Acquire(uri);
        var provider = new SftpProvider(factory, uri, null, lease);
        var name = "colon:name\\part% literal.txt";
        var entry = new SftpEntry(
            provider,
            new SftpMetadata(name, 1 | 4, 1, 0, 0, 0x81A4, 0, 0),
            FileProtocolEntryKind.File);

        Assert.That(provider.RemotePath(entry.Uri), Is.EqualTo("/root/" + name));
        Assert.Throws<ArgumentException>(() => provider.RemotePath(new Uri("sftp://user@[::1]:2222/root/a%2Fb")));
    }

    [Test]
    public async Task Authentication_IsBlockedUntilHostKeyApproval() {
        var uri = new Uri("sftp://user@127.0.0.1/");
        var client = new SftpClient(uri, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "known_hosts"));
        using var password = new SecureString();
        password.AppendChar('x');
        password.MakeReadOnly();
        try {
            var passwordAuthenticated = await client.AuthenticateByPassword(password, CancellationToken.None);
            var keyAuthenticated = await client.AuthenticateByKey(
                publicKeyFile: null,
                privateKeyFile: "unused-key-file",
                passphrase: password,
                token: CancellationToken.None);
            Assert.That(passwordAuthenticated, Is.False);
            Assert.That(keyAuthenticated, Is.False);
        }
        finally {
            await client.DisposeAsync();
        }
    }

    [TestCase(0x41EDu, true, false)]
    [TestCase(0x81A4u, false, false)]
    [TestCase(0xA1FFu, false, true)]
    public void PosixMode_ClassifiesDirectoryFilesAndSymlinks(uint mode, bool directory, bool symbolicLink) {
        Assert.That(SftpPosixMode.IsDirectory(mode), Is.EqualTo(directory));
        Assert.That(SftpPosixMode.IsSymbolicLink(mode), Is.EqualTo(symbolicLink));
    }

    [Test]
    public async Task NativeConnection_SetAddressAcceptsIpv4AndScopedIpv6() {
        var native = new SftpNative();
        Assert.That(await native.Loaded(CancellationToken.None), Is.True);
        using var connection = new NativeSftpConnection();
        Assert.DoesNotThrow(() => connection.SetAddress(IPAddress.Parse("192.0.2.10")));
        Assert.DoesNotThrow(() => connection.SetAddress(IPAddress.Parse("fe80::1%12")));
        await native.Freed(CancellationToken.None);
    }
}
