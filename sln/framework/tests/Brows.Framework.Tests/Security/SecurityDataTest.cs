using Brows.Composition;
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Brows.Security;

[TestFixture]
internal sealed class SecurityDataTest {
    private static SecureString Password(string text) {
        var password = new SecureString();
        foreach (var ch in text ?? "") {
            password.AppendChar(ch);
        }
        return password;
    }

    private static string Text(SecureString password) {
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
        try {
            return Marshal.PtrToStringUni(pointer);
        }
        finally {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    [Test]
    public void Work_WithoutEntropy_ReturnsFalse() {
        var subject = new SecureEntropy();

        var entropy = default(SecureString);
        var worked = ((IGetEntropy)subject).Work(set: value => entropy = value, CancellationToken.None).Result;

        Assert.That(worked, Is.False);
        Assert.That(entropy, Is.Null);
    }

    [Test]
    public void Work_WithEntropy_ReturnsACopyOfTheSetValue() {
        var subject = new SecureEntropy();
        ((ISetEntropy)subject).Work(Password("entropy"));

        var first = default(SecureString);
        var second = default(SecureString);
        var firstWorked = ((IGetEntropy)subject).Work(set: value => first = value, CancellationToken.None).Result;
        var secondWorked = ((IGetEntropy)subject).Work(set: value => second = value, CancellationToken.None).Result;

        Assert.Multiple(() => {
            Assert.That(firstWorked, Is.True);
            Assert.That(secondWorked, Is.True);
            Assert.That(Text(first), Is.EqualTo("entropy"));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(Text(second), Is.EqualTo("entropy"));
        });
    }

    [Test]
    public void Work_AfterEntropyIsCleared_ReturnsFalse() {
        var subject = new SecureEntropy();
        ((ISetEntropy)subject).Work(Password("entropy"));
        ((ISetEntropy)subject).Work(null);

        var entropy = default(SecureString);
        var worked = ((IGetEntropy)subject).Work(set: value => entropy = value, CancellationToken.None).Result;

        Assert.That(worked, Is.False);
        Assert.That(entropy, Is.Null);
    }

    [Test]
    public Task Protect_WithoutEntropy_ThrowsEntropyDidNotWork() {
        var entropy = new SecureEntropy();
        return ImportSandbox.Enter([new TestProtectData(), new TestUnprotectData(), entropy], async () => {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SecurityData.Protect("value", CancellationToken.None));

            Assert.That(error.Message, Is.EqualTo("Entropy did not work."));
        });
    }

    [Test]
    public Task Protect_WithEntropy_RoundTripsThroughUnprotect() {
        var secureEntropy = new SecureEntropy();
        return ImportSandbox.Enter([new TestProtectData(), new TestUnprotectData(), secureEntropy], async () => {
            ((ISetEntropy)secureEntropy).Work(Password("entropy"));

            var protectedData = await SecurityData.Protect("value", CancellationToken.None);
            var unprotectedData = await SecurityData.Unprotect(protectedData, CancellationToken.None);

            Assert.That(unprotectedData, Is.EqualTo("value"));
        });
    }

    [Test]
    public Task Protect_WithExplicitEntropy_RoundTripsThroughUnprotect() {
        return ImportSandbox.Enter([new TestProtectData(), new TestUnprotectData()], async () => {
            var data = new byte[] { 1, 2, 3, 4, 5 };

            var protectedData = await SecurityData.Protect(data, Password("entropy"), CancellationToken.None);
            var unprotectedData = await SecurityData.Unprotect(protectedData, Password("entropy"), CancellationToken.None);

            Assert.That(unprotectedData, Is.EqualTo(data));
        });
    }

    [Test]
    public Task Unprotect_WithTheWrongEntropy_ThrowsCryptographicException() {
        return ImportSandbox.Enter([new TestProtectData(), new TestUnprotectData()], async () => {
            var protectedData = await SecurityData.Protect("value", Password("entropy"), CancellationToken.None);

            await Assert.ThrowsAsync<CryptographicException>(
                () => SecurityData.Unprotect(protectedData, Password("wrong"), CancellationToken.None));
        });
    }

    [Test]
    public void EncodeSecret_DecodeSecret_RoundTripsUsernameAndPassword() {
        var encoded = SecurityData.EncodeSecret("user", Password("pass"));

        var decoded = SecurityData.DecodeSecret(encoded);

        Assert.Multiple(() => {
            Assert.That(decoded.Username, Is.EqualTo("user"));
            Assert.That(Text(decoded.Password), Is.EqualTo("pass"));
            Assert.That(decoded.Password.IsReadOnly(), Is.True);
        });
    }

    [Test]
    public void EncodeSecret_DecodeSecret_RoundTripsPasswordOnly() {
        var encoded = SecurityData.EncodeSecret(null, Password("pass"));

        var decoded = SecurityData.DecodeSecret(encoded);

        Assert.Multiple(() => {
            Assert.That(decoded.Username, Is.Empty);
            Assert.That(Text(decoded.Password), Is.EqualTo("pass"));
        });
    }

    /*
     * The fake protection prefixes the data with a check byte before
     * applying the entropy, so that unprotecting with the wrong entropy
     * fails the way the real implementation does.
     */
    private sealed class TestProtectData : IProtectData {
        Task<bool> IProtectData.Work(byte[] data, byte[] entropy, Action<byte[]> set, CancellationToken token) {
            if (set is null) {
                return Task.FromResult(false);
            }
            var source = new byte[(data?.Length ?? 0) + 1];
            source[0] = 170;
            data?.CopyTo(source, 1);
            var result = new byte[source.Length + 1];
            result[0] = 66;
            for (var i = 0; i < source.Length; i++) {
                result[i + 1] = (byte)(source[i] ^ entropy[i % entropy.Length]);
            }
            set(result);
            return Task.FromResult(true);
        }
    }

    private sealed class TestUnprotectData : IUnprotectData {
        Task<bool> IUnprotectData.Work(byte[] data, byte[] entropy, Action<byte[]> set, CancellationToken token) {
            if (set is null) {
                return Task.FromResult(false);
            }
            if (data.Length < 2 || data[0] != 66) {
                throw new CryptographicException("The data was not protected.");
            }
            var source = new byte[data.Length - 1];
            for (var i = 0; i < source.Length; i++) {
                source[i] = (byte)(data[i + 1] ^ entropy[i % entropy.Length]);
            }
            if (source.Length < 1 || source[0] != 170) {
                throw new CryptographicException("The entropy did not match.");
            }
            var result = new byte[source.Length - 1];
            Array.Copy(source, 1, result, 0, result.Length);
            set(result);
            return Task.FromResult(true);
        }
    }
}
