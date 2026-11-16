using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Connection;

[TestFixture]
internal sealed class SftpAuthenticationCoordinatorTest {
    private static SecureString Secret(string value) {
        var secret = new SecureString();
        foreach (var character in value) {
            secret.AppendChar(character);
        }
        secret.MakeReadOnly();
        return secret;
    }

    [Test]
    public async Task Authenticate_UnencryptedConfiguredKeyCanSucceedWithoutPrompting() {
        var passwordPromptCalled = false;
        var passphrasePromptCalled = false;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            ["key.pem"],
            _ => Task.FromResult(true),
            (_, _) => {
                passwordPromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _) => Task.FromResult(false),
            (_, _, _) => {
                passphrasePromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _, _) => Task.FromResult(false),
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Authenticated));
        Assert.That(passwordPromptCalled, Is.False);
        Assert.That(passphrasePromptCalled, Is.False);
    }

    [Test]
    public async Task Authenticate_AccountPasswordFallbackSkipsKeysWhenNoneAreConfigured() {
        var password = Secret("account-password");
        var passphrasePromptCalled = false;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            [],
            _ => Task.FromResult(false),
            (_, _) => Task.FromResult(password),
            (secret, _) => Task.FromResult(ReferenceEquals(secret, password)),
            (_, _, _) => {
                passphrasePromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _, _) => Task.FromResult(false),
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Authenticated));
        Assert.That(passphrasePromptCalled, Is.False);
        Assert.Throws<ObjectDisposedException>(() => password.Copy());
    }

    [Test]
    public async Task Authenticate_EncryptedKeySuccessDoesNotPromptForPassword() {
        var passphrase = Secret("key-passphrase");
        var passwordPromptCalled = false;
        var passwordAuthenticationCalled = false;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            ["key.pem"],
            _ => Task.FromResult(false),
            (_, _) => {
                passwordPromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _) => {
                passwordAuthenticationCalled = true;
                return Task.FromResult(false);
            },
            (host, keyFile, _) => {
                Assert.That(host, Is.EqualTo("sftp.example.test"));
                Assert.That(keyFile, Is.EqualTo("key.pem"));
                return Task.FromResult(passphrase);
            },
            (_, secret, _) => Task.FromResult(ReferenceEquals(secret, passphrase)),
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Authenticated));
        Assert.That(passwordPromptCalled, Is.False);
        Assert.That(passwordAuthenticationCalled, Is.False);
        Assert.Throws<ObjectDisposedException>(() => passphrase.Copy());
    }

    [Test]
    public async Task Authenticate_UsesSeparateSecretsForPasswordAndEachConfiguredKey() {
        var password = Secret("account-password");
        var firstPassphrase = Secret("first-key-passphrase");
        var secondPassphrase = Secret("second-key-passphrase");
        var configuredKeys = new[] { "first.key", "second.key" };
        var promptedKeys = new List<string>();
        var passwordAttempts = new List<SecureString>();
        var keyAttempts = new List<(string KeyFile, SecureString Passphrase)>();
        var authenticationOrder = new List<string>();
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            configuredKeys,
            _ => {
                authenticationOrder.Add("unencrypted");
                return Task.FromResult(false);
            },
            (host, _) => {
                authenticationOrder.Add("password-prompt");
                Assert.That(host, Is.EqualTo("sftp.example.test"));
                return Task.FromResult(password);
            },
            (secret, _) => {
                authenticationOrder.Add("password-auth");
                passwordAttempts.Add(secret);
                return Task.FromResult(true);
            },
            (host, keyFile, _) => {
                authenticationOrder.Add($"passphrase-prompt:{keyFile}");
                Assert.That(host, Is.EqualTo("sftp.example.test"));
                promptedKeys.Add(keyFile);
                var passphrase = keyFile == "first.key" ? firstPassphrase : secondPassphrase;
                return Task.FromResult(passphrase);
            },
            (keyFile, passphrase, _) => {
                authenticationOrder.Add($"key-auth:{keyFile}");
                keyAttempts.Add((keyFile, passphrase));
                return Task.FromResult(false);
            },
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Authenticated));
        Assert.That(passwordAttempts.Count, Is.EqualTo(1));
        Assert.That(passwordAttempts[0], Is.SameAs(password));
        Assert.That(promptedKeys, Is.EqualTo(configuredKeys));
        Assert.That(keyAttempts.Count, Is.EqualTo(2));
        Assert.That(keyAttempts[0].KeyFile, Is.EqualTo("first.key"));
        Assert.That(keyAttempts[0].Passphrase, Is.SameAs(firstPassphrase));
        Assert.That(keyAttempts[0].Passphrase, Is.Not.SameAs(password));
        Assert.That(keyAttempts[1].KeyFile, Is.EqualTo("second.key"));
        Assert.That(keyAttempts[1].Passphrase, Is.SameAs(secondPassphrase));
        Assert.That(keyAttempts[1].Passphrase, Is.Not.SameAs(password));
        Assert.That(authenticationOrder, Is.EqualTo(new[] {
            "unencrypted",
            "passphrase-prompt:first.key",
            "key-auth:first.key",
            "passphrase-prompt:second.key",
            "key-auth:second.key",
            "password-prompt",
            "password-auth"
        }));
        Assert.Throws<ObjectDisposedException>(() => password.Copy());
        Assert.Throws<ObjectDisposedException>(() => firstPassphrase.Copy());
        Assert.Throws<ObjectDisposedException>(() => secondPassphrase.Copy());
    }

    [Test]
    public async Task Authenticate_AccountPasswordPromptCancellationSkipsAuthentication() {
        var passwordAuthenticationCalled = false;
        var passphrasePromptCalled = false;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            [],
            _ => Task.FromResult(false),
            (_, _) => Task.FromResult<SecureString>(null),
            (_, _) => {
                passwordAuthenticationCalled = true;
                return Task.FromResult(false);
            },
            (_, _, _) => {
                passphrasePromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _, _) => Task.FromResult(false),
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Canceled));
        Assert.That(passwordAuthenticationCalled, Is.False);
        Assert.That(passphrasePromptCalled, Is.False);
    }

    [Test]
    public async Task Authenticate_PrivateKeyPassphraseCancellationStopsBeforePasswordFallback() {
        var passwordPromptCalled = false;
        var keyAuthenticationCalled = false;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            ["key.pem"],
            _ => Task.FromResult(false),
            (_, _) => {
                passwordPromptCalled = true;
                return Task.FromResult<SecureString>(null);
            },
            (_, _) => Task.FromResult(false),
            (_, _, _) => Task.FromResult<SecureString>(null),
            (_, _, _) => {
                keyAuthenticationCalled = true;
                return Task.FromResult(false);
            },
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Canceled));
        Assert.That(passwordPromptCalled, Is.False);
        Assert.That(keyAuthenticationCalled, Is.False);
    }

    [Test]
    public async Task Authenticate_PropagatesTokenCancellationAfterKeyFailure() {
        using var cancellation = new CancellationTokenSource();
        var passphrase = Secret("key-passphrase");
        var passwordPromptCalled = false;

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await SftpAuthenticationCoordinator.Authenticate(
                "sftp.example.test",
                ["key.pem"],
                _ => Task.FromResult(false),
                (_, _) => {
                    passwordPromptCalled = true;
                    return Task.FromResult<SecureString>(null);
                },
                (_, _) => Task.FromResult(false),
                (_, _, _) => Task.FromResult(passphrase),
                (_, _, _) => {
                    cancellation.Cancel();
                    return Task.FromResult(false);
                },
                cancellation.Token));

        Assert.That(passwordPromptCalled, Is.False);
        Assert.Throws<ObjectDisposedException>(() => passphrase.Copy());
    }

    [Test]
    public async Task Authenticate_ReportsFailureAfterPasswordAndConfiguredKeysFail() {
        var passphrase = Secret("key-passphrase");
        var password = Secret("account-password");
        var keyAttempts = 0;
        var result = await SftpAuthenticationCoordinator.Authenticate(
            "sftp.example.test",
            ["key.pem"],
            _ => Task.FromResult(false),
            (_, _) => Task.FromResult(password),
            (_, _) => Task.FromResult(false),
            (_, _, _) => Task.FromResult(passphrase),
            (_, _, _) => {
                keyAttempts++;
                return Task.FromResult(false);
            },
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(SftpAuthenticationResult.Failed));
        Assert.That(keyAttempts, Is.EqualTo(1));
        Assert.Throws<ObjectDisposedException>(() => password.Copy());
        Assert.Throws<ObjectDisposedException>(() => passphrase.Copy());
    }
}
