using Brows.DirectoryListing;
using Brows.Url;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FtpClient : UrlClient {
    private static readonly ILog Log = Logging.For(typeof(FtpClient));

    private sealed class CommandException : IOException {
        public bool IsNotDirectory { get; }

        public CommandException(bool isNotDirectory, string message, Exception innerException)
            : base(message, innerException) {
            IsNotDirectory = isNotDirectory;
        }
    }

    private static Uri ChangeScheme(Uri uri, string scheme, int defaultPort) {
        var builder = new UriBuilder(uri) {
            Scheme = scheme,
            Port = uri.IsDefaultPort ? defaultPort : uri.Port
        };
        return builder.Uri;
    }

    private static string CommandPathFromUriPath(string path) {
        ArgumentNullException.ThrowIfNull(path);
        var hasUriPathPrefix = path.StartsWith('/');
        var hasPathSegments = path.Trim('/').Length > 0;
        var hasTooManyLeadingSlashes = path.StartsWith("///", StringComparison.Ordinal);
        var pathIsInvalid = hasUriPathPrefix == false || hasPathSegments == false || hasTooManyLeadingSlashes;
        if (pathIsInvalid) {
            throw new ArgumentException(
                "The FTP command path must be a non-root FTP URI path.",
                nameof(path));
        }

        /*
         * Brows keeps the leading slash required by an FTP URI. libcurl interprets one slash as relative to
         * the login directory and two slashes as server-absolute. Removing exactly one slash when forming the
         * FTP command argument preserves that distinction: relative paths stay relative and absolute paths
         * retain one leading slash.
         */
        return path[1..];
    }

    internal static FtpTlsMode GetTlsMode(Uri uri, FtpTlsMode mode) {
        if (uri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase)) {
            return FtpTlsMode.Explicit;
        }
        var isImplicitFtps = uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
        var explicitTls = uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Any(parts => parts.Length == 2 &&
                          Uri.UnescapeDataString(parts[0]).Equals("brows-tls", StringComparison.OrdinalIgnoreCase) &&
                          Uri.UnescapeDataString(parts[1]).Equals("explicit", StringComparison.OrdinalIgnoreCase));
        if (explicitTls) {
            return FtpTlsMode.Explicit;
        }
        /*
         * A configured None cannot turn an implicit FTPS URL into clear FTP data.
         */
        var configuredNoneForImplicitFtps = mode == FtpTlsMode.None && isImplicitFtps;
        if (configuredNoneForImplicitFtps) {
            return FtpTlsMode.Implicit;
        }
        if (mode != FtpTlsMode.Auto) {
            return mode;
        }
        if (isImplicitFtps) {
            return FtpTlsMode.Implicit;
        }
        return FtpTlsMode.None;
    }

    internal sealed override Task<Uri> ResolveUri(Uri uri, CancellationToken token) {
        var tlsMode = GetTlsMode(Uri, TlsMode);
        if (tlsMode != FtpTlsMode.None) {
            return Task.FromResult(uri);
        }
        return base.ResolveUri(uri, token);
    }

    internal Uri CommandUri => new UriBuilder(Uri) { Path = "/", Query = "", Fragment = "" }.Uri;

    internal Uri UploadUri(string path) {
        ArgumentNullException.ThrowIfNull(path);
        var hasUriPathPrefix = path.StartsWith('/');
        var hasPathSegments = path.Trim('/').Length > 0;
        var hasTooManyLeadingSlashes = path.StartsWith("///", StringComparison.Ordinal);
        var pathIsInvalid = hasUriPathPrefix == false || hasPathSegments == false || hasTooManyLeadingSlashes;
        if (pathIsInvalid) {
            throw new ArgumentException("The FTP upload path must be a non-root FTP URI path.", nameof(path));
        }

        var uriPathPrefix = path.StartsWith("//", StringComparison.Ordinal) ? "//" : "/";
        var encodedPath = uriPathPrefix + string.Join('/', path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        return new UriBuilder(Uri) { Path = encodedPath, Query = "", Fragment = "" }.Uri;
    }

    internal FtpTlsMode EffectiveTlsMode => GetTlsMode(Uri, TlsMode);

    protected sealed override void Configure(ClientForUrl client) {
        ArgumentNullException.ThrowIfNull(client);
        var tlsMode = EffectiveTlsMode;
        client.FtpTls(tlsMode switch {
            FtpTlsMode.None => FtpNativeTlsMode.None,
            FtpTlsMode.Explicit => FtpNativeTlsMode.Explicit,
            FtpTlsMode.Implicit => FtpNativeTlsMode.Implicit,
            _ => throw new ArgumentOutOfRangeException(nameof(TlsMode))
        });
        var certificateAuthorityFile = CertificateAuthorityFile;
        if (!string.IsNullOrWhiteSpace(certificateAuthorityFile)) {
            client.FtpCertificateAuthorityFile(certificateAuthorityFile);
        }
    }

    protected sealed override Uri TransferUri(Uri uri) {
        return GetTransferUri(uri);
    }

    internal Uri GetTransferUri(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var tlsMode = GetTlsMode(Uri, TlsMode);
        var cleanUri = new UriBuilder(uri) { Query = "", Fragment = "" }.Uri;
        if (tlsMode == FtpTlsMode.Explicit &&
            (uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase))) {
            return ChangeScheme(cleanUri, "ftp", 21);
        }
        if (tlsMode == FtpTlsMode.Implicit && uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase)) {
            return ChangeScheme(cleanUri, "ftps", 990);
        }
        return cleanUri;
    }

    public FtpTlsMode TlsMode { get; set; } = FtpTlsMode.Auto;

    public string CertificateAuthorityFile { get; set; }

    public DirectoryListServerType ListingServerType { get; set; }

    public FtpClient() {
    }

    public Task CreateDirectory(string path, CancellationToken token) {
        return Command(FtpClientCommand.MakeDirectory, path, null, token);
    }

    internal async Task EnsureDirectory(string path, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        try {
            await CreateDirectory(path, token);
            return;
        }
        catch (IOException createFailure) {
            token.ThrowIfCancellationRequested();
            try {
                await Command(FtpClientCommand.ChangeDirectory, path, null, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                throw;
            }
            catch (IOException checkFailure) {
                throw new IOException(
                    $"The FTP server could not create or verify directory '{path}'.",
                    new AggregateException(createFailure, checkFailure));
            }
        }
    }

    public Task Delete(string path, bool directory, CancellationToken token) {
        return Command(directory ? FtpClientCommand.RemoveDirectory : FtpClientCommand.DeleteFile, path, null, token);
    }

    internal async Task<bool> IsDirectory(string path, CancellationToken token) {
        try {
            await Command(FtpClientCommand.ChangeDirectory, path, null, token);
            return true;
        }
        catch (CommandException exception) when (exception.IsNotDirectory) {
            return false;
        }
    }

    public Task Rename(string source, string destination, CancellationToken token) {
        return Command(FtpClientCommand.Rename, source, destination, token);
    }

    public Task<Stream> Write(string path, long length, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("FTP upload", path, length));
        }
        return WriteUpload(length, token, UploadUri(path));
    }

    internal async Task CreateFile(string path, string name, CancellationToken token) {
        /*
         * FTP has no portable exclusive-create command. The listing refuses a name that already exists;
         * a zero-byte APPE keeps a file added after that listing from being truncated.
         */
        await CheckFileNameDoesNotExist(name, token);
        await using (var stream = await CreateFileUpload(path, token)) {
            await stream.FlushAsync(token);
        }
    }

    internal async Task CheckFileNameDoesNotExist(string name, CancellationToken token) {
        ArgumentException.ThrowIfNullOrEmpty(name);
        await foreach (var item in List(token).WithCancellation(token)) {
            if (string.Equals(item.Name, name, StringComparison.Ordinal)) {
                throw new IOException($"The FTP entry '{name}' already exists.");
            }
        }
    }

    internal Task<Stream> CreateFileUpload(string path, CancellationToken token) {
        return AppendUpload(0, UploadUri(path), token);
    }

    public IAsyncEnumerable<DirectoryListItem> List(CancellationToken token) {
        return DirectoryList.Parse(
            ListingContents(token),
            new DirectoryListOptions { ServerType = ListingServerType },
            token);
    }

    /*
     * Lists through the limiter that also governs downloads. The listing is buffered before the
     * connection's slot is released, so a slow consumer of the listing never holds a slot that a
     * download it is waiting for needs.
     */
    internal IAsyncEnumerable<DirectoryListItem> List(FtpTransferLimiter limiter, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(limiter);
        async Task<Stream> open(CancellationToken openToken) {
            await using var stream = await limiter.Open(
                Uri.UnescapeDataString(Uri.AbsolutePath),
                (header, readToken) => Read(null, header, readToken),
                openToken);
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, openToken);
            buffer.Position = 0;
            return buffer;
        }
        return DirectoryList.Parse(
            ListingContents(open, token),
            new DirectoryListOptions { ServerType = ListingServerType },
            token);
    }

    private static void LogCommandFailure(FtpClientCommand command,
                                          string firstPath,
                                          string secondPath,
                                          bool isNotDirectory,
                                          string error) {
        /*
         * A change of directory that finds a file is how directories are detected, so it is not a failure.
         */
        var expected = command == FtpClientCommand.ChangeDirectory && isNotDirectory;
        if (expected) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("FTP command found no directory", command, firstPath, error));
            }
            return;
        }
        if (Log.Warn()) {
            Log.Warn(Log.Join("FTP command failed", command, firstPath, secondPath, error));
        }
    }

    private async Task Command(FtpClientCommand command, string firstPath, string secondPath, CancellationToken token) {
        var firstCommandPath = CommandPathFromUriPath(firstPath);
        var secondCommandPath = secondPath is null ? null : CommandPathFromUriPath(secondPath);
        if (Log.Debug()) {
            Log.Debug(Log.Join("FTP command", command, firstCommandPath, secondCommandPath));
        }
        var client = await CreateClient(token, CommandUri);
        using (client) {
            var nativeCommand = command switch {
                FtpClientCommand.MakeDirectory => FtpNativeCommand.MakeDirectory,
                FtpClientCommand.ChangeDirectory => FtpNativeCommand.ChangeDirectory,
                FtpClientCommand.DeleteFile => FtpNativeCommand.DeleteFile,
                FtpClientCommand.RemoveDirectory => FtpNativeCommand.RemoveDirectory,
                FtpClientCommand.Rename => FtpNativeCommand.Rename,
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            await Task.Run(() => {
                token.ThrowIfCancellationRequested();
                try {
                    client.FtpCommand(nativeCommand, firstCommandPath, secondCommandPath);
                }
                catch (Exception ex) {
                    token.ThrowIfCancellationRequested();
                    var isNotDirectory = ex is NativeErrorException nativeException &&
                                         nativeException.Error == (int)FtpNativeError.NotDirectory;
                    var error = client.ErrorString();
                    LogCommandFailure(command, firstCommandPath, secondCommandPath, isNotDirectory, error);
                    throw new CommandException(isNotDirectory, error, ex);
                }
            }, token);
            if (Log.Info()) {
                Log.Info(Log.Join("FTP command succeeded", command, firstCommandPath, secondCommandPath));
            }
        }
    }
}
