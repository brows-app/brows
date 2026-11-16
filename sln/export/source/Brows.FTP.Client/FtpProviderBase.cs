using Brows.DirectoryListing;
using Brows.Panels.PanelSecrets;
using Brows.Url;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Brows;

internal abstract class FtpProviderBase : FileProtocolProvider<FtpEntry, FtpConfig>, IFileProtocolResourceIdentity {
    private static readonly ILog Log = Logging.For(typeof(FtpProviderBase));

    private readonly FtpData Data = new();
    private FtpClient Client => _Client ??= Factory.Client.Get(Uri, Config?.Client.Values);
    private FtpClient _Client;

    protected IFtpProviderFactory Factory { get; }

    protected FtpProviderBase(IFtpProviderFactory factory, Uri uri, object icon) : base(uri, 100, null, null) {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Icon = icon;
        Transfers = new(name: uri.Scheme + "://" + uri.Authority);
    }

    protected sealed override async Task List(CancellationToken token) {
        var client = Client;
        var credential = client.Credential;
        if (Log.Info()) {
            Log.Info(Log.Join("FTP listing", UrlClient.Redact(Uri)));
        }
        if (credential?.Password is null) {
            if (Log.Info()) {
                Log.Info(Log.Join("FTP credentials requested", client.Uri.IdnHost));
            }
            var secret = await GetSecret<ISecretUsernameAndPassword>(
                $"ftp://{client.Uri.IdnHost}",
                "FtpProvider_Prompt_UsernameAndPassword",
                new[] { client.Url },
                token);
            var securePassword = secret?.Password;
            if (securePassword is not null) {
                try {
                    var username = secret.Username;
                    if (credential is not null) {
                        if (!string.IsNullOrEmpty(username)) {
                            credential.Username = username;
                        }
                        var protect = credential.ProtectPassword(securePassword, SecurityDataKind.Protected, token);
                        await protect;
                    }
                }
                finally {
                    securePassword.Dispose();
                }
            }
        }
        ViewRaw = true;
        var read = Data.Read(client, token);
        var count = 0;
        await foreach (var info in client.List(token).WithCancellation(token)) {
            await Provide(new FtpEntry(this, info), token);
            ViewRaw = false;
            count++;
        }
        await read;
        if (Log.Info()) {
            Log.Info(Log.Join("FTP listed", UrlClient.Redact(Uri), count));
        }
    }

    public bool ViewRaw {
        get => _ViewRaw.Value;
        set => Change(_ViewRaw, value);
    }
    private readonly Notified<bool> _ViewRaw = new(nameof(ViewRaw));

    public object Headers => Data.Headers;
    public object Listing => Data.Listing;
    public object Icon { get; }
    internal EntryStreamGuiOptions StreamOptions => Config?.Stream ?? (field ??= new());
    internal FtpTransferLimiter Transfers { get; }

    public FtpClient ClientFor(FtpEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        return ClientAt(entry.Uri);
    }

    public FtpClient ClientAt(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return Factory.Client.Get(uri, Config?.Client.Values);
    }

    internal Uri ResourceIdentity(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var client = ClientAt(uri);
        var transferUri = client.GetTransferUri(uri);
        var userName = EffectiveUsername(client, uri);
        return new UriBuilder(transferUri) {
            UserName = Uri.EscapeDataString(userName),
            Password = "",
            Query = "",
            Fragment = ""
        }.Uri;
    }

    private static string EffectiveUsername(FtpClient client, Uri uri) {
        var configuredUsername = client.Credential.Username;
        if (!string.IsNullOrEmpty(configuredUsername)) {
            return configuredUsername;
        }
        var userInfo = uri.UserInfo;
        var passwordSeparator = userInfo.IndexOf(':');
        var escapedUsername = passwordSeparator < 0
            ? userInfo
            : userInfo[..passwordSeparator];
        var username = Uri.UnescapeDataString(escapedUsername);
        return username.Length == 0 ? "anonymous" : username;
    }

    internal bool HasSameRenameProfile(FtpProviderBase other, Uri uri, Uri otherUri) {
        if (other is null) {
            return false;
        }
        var client = ClientAt(uri);
        var otherClient = other.ClientAt(otherUri);
        var firstCredentials = RenameCredentials(client, uri);
        var secondCredentials = RenameCredentials(otherClient, otherUri);
        var sameCredentials = firstCredentials == secondCredentials;
        var sameTls = client.EffectiveTlsMode == otherClient.EffectiveTlsMode;
        var sameCertificateAuthority = string.Equals(
            client.CertificateAuthorityFile,
            otherClient.CertificateAuthorityFile,
            StringComparison.Ordinal);
        var sameResolution = client.Resolve == otherClient.Resolve;
        return sameCredentials && sameTls && sameCertificateAuthority && sameResolution;
    }

    private static (string Username, string Password, SecurityDataKind? PasswordKind) RenameCredentials(
        FtpClient client,
        Uri uri) {
        var userInfo = uri.UserInfo;
        var passwordSeparator = userInfo.IndexOf(':');
        var escapedPassword = passwordSeparator < 0
            ? ""
            : userInfo[(passwordSeparator + 1)..];
        var uriPassword = Uri.UnescapeDataString(escapedPassword);
        var configuredPassword = client.Credential.Password;
        var hasConfiguredPassword = !string.IsNullOrEmpty(configuredPassword);
        var password = hasConfiguredPassword ? configuredPassword : uriPassword;
        SecurityDataKind? passwordKind = hasConfiguredPassword ? client.Credential.PasswordKind : null;
        return (EffectiveUsername(client, uri), password, passwordKind);
    }

    internal virtual Task<bool> IsDirectoryTarget(FtpEntry entry, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        return ClientFor(entry).IsDirectory(entry.RemotePath, token);
    }

    internal virtual Task<Stream> Read(FtpEntry entry, long? length, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Client().Read(length, token);
    }

    internal virtual Task<Stream> Download(FtpEntry entry, long? length, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        var client = entry.Client();
        if (Log.Info()) {
            Log.Info(Log.Join("FTP download", entry.RemotePath, length));
        }
        return Transfers.Open(
            entry.RemotePath,
            (header, readToken) => client.Read(length, header, readToken),
            token);
    }

    internal virtual async IAsyncEnumerable<FtpEntry> ListEntries(
        Uri uri,
        [EnumeratorCancellation] CancellationToken token) {
        ArgumentNullException.ThrowIfNull(uri);
        var relativePath = FileProtocolUri.RelativePath(Uri, uri);
        var listClient = Factory.Client.Get(uri, Config.Client.Values);
        if (Log.Debug()) {
            Log.Debug(Log.Join("FTP listing entries", UrlClient.Redact(uri)));
        }
        await foreach (var item in listClient.List(Transfers, token).WithCancellation(token)) {
            token.ThrowIfCancellationRequested();
            if (item.Name is "." or "..") {
                continue;
            }
            var entryUri = FileProtocolUri.Extend(
                Uri,
                [relativePath, item.Name],
                includeTrailingSlash: item.EntryKind() == FileProtocolEntryKind.Directory);
            yield return new FtpEntry(this, item, entryUri);
        }
    }

    internal async Task<int> DeleteRecursive(FtpEntry entry, Action<FtpEntry> deleted, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(deleted);
        if (!ReferenceEquals(entry.Provider, this)) {
            throw new ArgumentException("The entry belongs to a different provider.", nameof(entry));
        }
        if (entry.Name is "." or "..") {
            throw new InvalidDataException("FTP listings cannot delete dot-directory entries.");
        }

        var deletedCount = 0;
        async Task Delete(FtpEntry item) {
            token.ThrowIfCancellationRequested();
            var isDirectory = item.Kind switch {
                FileProtocolEntryKind.Directory => true,
                FileProtocolEntryKind.File or
                FileProtocolEntryKind.FileLink or
                FileProtocolEntryKind.DirectoryLink => false,
                _ => throw new NotSupportedException($"Deleting {item.Kind} FTP entries is not supported.")
            };
            if (item.Kind == FileProtocolEntryKind.Directory) {
                await foreach (var child in ListEntries(item.Uri, token).WithCancellation(token)) {
                    if (child.Name is "." or "..") {
                        continue;
                    }
                    await Delete(child);
                }
            }
            token.ThrowIfCancellationRequested();
            await DeleteEntry(item, isDirectory, token);
            deleted(item);
            deletedCount++;
        }

        await Delete(entry);
        return deletedCount;
    }

    internal virtual Task DeleteEntry(FtpEntry entry, bool directory, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        return ClientFor(entry).Delete(entry.RemotePath, directory, token);
    }

    protected abstract class FtpProvideIO<TProvider> : FileProtocolProvider<FtpEntry, FtpConfig>.ProvideIO<TProvider>
    where TProvider : FtpProviderBase {
        protected sealed override IAsyncEnumerable<FtpEntry>
        List(TProvider provider, Uri uri, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(provider);
            return provider.ListEntries(uri, token);
        }

        protected sealed override FileProtocolStreamSource<FtpEntry> Read(TProvider provider, FtpEntry entry) {
            return new FtpStreamSource(entry);
        }
    }

    protected abstract class FtpCopyIO<TProvider> : FileProtocolProvider<FtpEntry, FtpConfig>.CopyProvidedIO<TProvider>
    where TProvider : FtpProviderBase {
        protected sealed override Task CreateDirectory(TProvider provider, string path, CancellationToken token) {
            var client = provider.Factory.Client.Get(provider.Uri, provider.Config.Client.Values);
            return client.CreateDirectory(path, token);
        }

        protected sealed override Task EnsureDirectory(TProvider provider, string path, CancellationToken token) {
            var client = provider.Factory.Client.Get(provider.Uri, provider.Config.Client.Values);
            return client.EnsureDirectory(path, token);
        }

        protected sealed override Task<Stream> Write(TProvider provider,
                                                      string path,
                                                      long length,
                                                      CancellationToken token) {
            var client = provider.Factory.Client.Get(provider.Uri, provider.Config.Client.Values);
            return client.Write(path, length, token);
        }
    }

    protected abstract class FtpMoveProvidedIO<TProvider> : IMoveProvidedIO, IProviderExport<TProvider>
    where TProvider : FtpProviderBase {
        protected abstract IWorkProvidedIO Copy { get; }

        async Task<bool> IWorkProvidedIO.Work(IEnumerable<IProvidedIO> io,
                                               IProvider target,
                                               IOperationProgress progress,
                                               CancellationToken token) {
            ArgumentNullException.ThrowIfNull(io);
            if (target is not TProvider provider) {
                return false;
            }

            var providedIO = io.ToList();
            var sources = providedIO.FileSources();
            if (sources.Count == 0) {
                return false;
            }
            var snapshot = LocalFileMove.Capture(sources, token);

            var copied = await Copy.Work(providedIO, provider, progress, token);
            if (!copied) {
                return false;
            }

            LocalFileMove.DeleteUnchanged(
                sources,
                snapshot,
                "The local source changed during the move; the source was retained.",
                token);
            return true;
        }
    }

    Uri IFileProtocolResourceIdentity.GetResourceIdentity(Uri uri) {
        return ResourceIdentity(uri);
    }

}
