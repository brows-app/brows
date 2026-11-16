using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Brows.SFTP.Native;

[TestFixture]
internal sealed class SftpSecretBufferTest {
    private static SecureString CreateSecret(string value) {
        var result = new SecureString();
        foreach (var character in value) {
            result.AppendChar(character);
        }
        result.MakeReadOnly();
        return result;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(15)]
    [TestCase(16)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(63)]
    [TestCase(64)]
    [TestCase(127)]
    [TestCase(128)]
    public void Constructor_EncodesAndTerminatesAsciiAndClearsBufferOnDispose(int length) {
        var value = new string('a', length);
        var expected = Encoding.UTF8.GetBytes(value);
        var bytes = ReadOnlyMemory<byte>.Empty;
        using (var secret = CreateSecret(value)) {
            using (var buffer = new SftpSecretBuffer(secret)) {
                bytes = buffer.Bytes;
                var actual = new byte[buffer.Length + 1];
                Marshal.Copy(buffer.Pointer, actual, 0, actual.Length);

                Assert.That(buffer.Length, Is.EqualTo(expected.Length));
                Assert.That(actual, Is.EqualTo(expected.Concat(new byte[] { 0 }).ToArray()));
            }
        }

        Assert.That(bytes.ToArray().All(byteValue => byteValue == 0), Is.True);
    }

    [Test]
    public void Constructor_EncodesAndTerminatesMultibyteSecretAndClearsBufferOnDispose() {
        var value = "é漢😀";
        var expected = Encoding.UTF8.GetBytes(value);
        var bytes = ReadOnlyMemory<byte>.Empty;
        using (var secret = CreateSecret(value)) {
            using (var buffer = new SftpSecretBuffer(secret)) {
                bytes = buffer.Bytes;
                var actual = new byte[buffer.Length + 1];
                Marshal.Copy(buffer.Pointer, actual, 0, actual.Length);

                Assert.That(buffer.Length, Is.EqualTo(expected.Length));
                Assert.That(actual, Is.EqualTo(expected.Concat(new byte[] { 0 }).ToArray()));
            }
        }

        Assert.That(bytes.ToArray().All(byteValue => byteValue == 0), Is.True);
    }
}
