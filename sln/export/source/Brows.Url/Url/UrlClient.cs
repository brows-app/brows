using Brows.Url.Extensions;
using Brows.Url.IO;
using Domore.IO.Extensions;
using Domore.Logs;
using Domore.Text;
using Domore.Text.Builders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url;

public abstract class UrlClient {
    private static readonly ILog Log = Logging.For(typeof(UrlClient));
    private static int TransferCount;

    private static int NextTransfer() {
        return Interlocked.Increment(ref TransferCount);
    }

    private static void LogTransferResult(int transfer, Uri uri, Exception exception) {
        if (exception is null) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("Transfer", transfer, "Completed", Redact(uri)));
            }
            return;
        }
        if (exception is OperationCanceledException) {
            if (Log.Info()) {
                Log.Info(Log.Join("Transfer", transfer, "Canceled", Redact(uri)));
            }
            return;
        }
        if (Log.Warn()) {
            Log.Warn(Log.Join("Transfer", transfer, "Failed", Redact(uri), exception.Message));
        }
    }

    private void OnHeaderLine(int transfer, string line) {
        if (Log.Info()) {
            Log.Info(Log.Join("Transfer", transfer, line));
        }
        Header?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Header, text: line));
    }

    private void OnHeaderClear() {
        Header?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Header, clear: true));
    }

    private void OnHeaderComplete() {
        Header?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Header, complete: true));
    }

    private void OnListLine(string text) {
        if (Log.Info()) {
            Log.Info(text);
        }
        Listing?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Listing, text: text));
    }

    private void OnListClear() {
        Listing?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Listing, clear: true));
    }

    private void OnListComplete() {
        Listing?.Invoke(this, new UrlClientTextEventArgs(DateTime.UtcNow, UrlClientTextKind.Listing, complete: true));
    }

    internal virtual Task<Uri> ResolveUri(Uri uri, CancellationToken token) {
        var resolve = Resolve;
        return resolve
            ? UrlHost.Resolve(uri, token)
            : Task.FromResult(uri);
    }

    private async Task<ClientForUrl> CreateClient(int transfer, CancellationToken token, Uri uri) {
        uri ??= Uri;
        var resolvedUri = await ResolveUri(uri, token);
        resolvedUri = TransferUri(resolvedUri);
        if (Log.Info()) {
            Log.Info(resolvedUri.Equals(uri)
                ? Log.Join("Transfer", transfer, "Using", Redact(uri))
                : Log.Join("Transfer", transfer, "Resolved", Redact(uri), "to", Redact(resolvedUri)));
        }
        var client = new ClientForUrl(resolvedUri);
        client.OnProgress(token);
        var credential = Credential;
        if (credential != null) {
            var username = credential.Username;
            if (username != null && username != "") {
                if (Log.Info()) {
                    Log.Info(Log.Join("Transfer", transfer, "Setting username for", Redact(resolvedUri)));
                }
                client.Username(username);
            }
            var password = credential.Password;
            if (password != null && password != "") {
                if (Log.Info()) {
                    Log.Info(Log.Join("Transfer", transfer, "Setting password for", Redact(resolvedUri)));
                }
                client.Password(await credential.UnprotectPassword(token));
            }
        }
        Configure(client);
        return client;
    }

    protected Task<ClientForUrl> CreateClient(CancellationToken token, Uri uri = null) {
        return CreateClient(NextTransfer(), token, uri);
    }

    protected virtual Uri TransferUri(Uri uri) {
        return uri;
    }

    protected Task<Stream> WriteUpload(long length, CancellationToken token, Uri uri = null) {
        return WriteUpload(length, uri, append: false, token: token);
    }

    internal Task<Stream> AppendUpload(long length, Uri uri, CancellationToken token) {
        return WriteUpload(length, uri, append: true, token: token);
    }

    private async Task<Stream> WriteUpload(long length, Uri uri, bool append, CancellationToken token) {
        var client = await CreateClient(token, uri);
        try {
            return new ClientUploadStream(client, length, append, token);
        }
        catch {
            client.Dispose();
            throw;
        }
    }

    internal async IAsyncEnumerable<string> ListingContents(Func<CancellationToken, Task<Stream>> open,
                                                            [EnumeratorCancellation] CancellationToken token) {
        ArgumentNullException.ThrowIfNull(open);
        using var stream = await open(token);
        var streamText = ClientStreamText.Create(stream);
        var streamTextBuilder = new TextStreamBuilder();
        var streamLineBuilder = new TextLineBuilder(
            onLine: OnListLine,
            onClear: OnListClear,
            onComplete: OnListComplete);
        var streamBuilders = new DecodedTextBuilder[] { streamTextBuilder, streamLineBuilder };
        var streamTextTask = streamText.DecodeText(streamBuilders, Text, token);
        await foreach (var item in streamTextBuilder.Read(token)) {
            if (item.Clear == false) {
                yield return item.Text;
            }
        }
        await streamTextTask;
    }

    /*
     * Returns the URI with any password replaced by asterisks, for logging.
     */
    internal static string Redact(Uri uri) {
        if (uri is null) {
            return null;
        }
        var hasPassword = uri.UserInfo.Contains(':', StringComparison.Ordinal);
        if (hasPassword is false) {
            return uri.ToString();
        }
        return new UriBuilder(uri) { Password = "****" }.Uri.ToString();
    }

    internal object Clone() {
        return MemberwiseClone();
    }

    protected virtual void Configure(ClientForUrl client) {
    }

    protected IAsyncEnumerable<string> ListingContents(CancellationToken token) {
        return ListingContents(openToken => Read(null, openToken), token);
    }

    public event UrlClientTextEventHandler Header;
    public event UrlClientTextEventHandler Listing;

    public Uri Uri {
        get => _Uri ??= new Uri(Url);
        set {
            if (_Uri != value) {
                _Uri = value;
                _Url = _Uri?.ToString();
            }
        }
    }
    private Uri _Uri;

    public string Url {
        get => _Url;
        set {
            if (_Url != value) {
                _Url = value;
                _Uri = null;
            }
        }
    }
    private string _Url;

    public bool Resolve { get; set; } = true;

    public SecurityCredential Credential {
        get => _Credential ??= new();
        set => _Credential = value;
    }
    private SecurityCredential _Credential;

    public DecodedTextOptions Text {
        get => _Text ??= new();
        set => _Text = value;
    }
    private DecodedTextOptions _Text;

    public Task<Stream> Read(long? length, CancellationToken token) {
        return Read(length, null, token);
    }

    /*
     * The header callback receives each response line of this transfer. Every line has been delivered
     * before the returned stream completes, so a reader that observes a failure has seen its responses.
     */
    internal async Task<Stream> Read(long? length, Action<string> header, CancellationToken token) {
        var transfer = NextTransfer();
        var uri = Uri;
        var client = await CreateClient(transfer, token, uri);
        var writeStream = ClientStream.Create(client, ClientStreamKind.Write, length ?? -1);
        var headerStream = ClientStream.Create(client, ClientStreamKind.Header, length ?? -1);
        var headerStreamText = ClientStreamText.Create(headerStream);
        var headerLineBuilder = new TextLineBuilder(
            onLine: line => {
                header?.Invoke(line);
                OnHeaderLine(transfer, line);
            },
            onClear: OnHeaderClear,
            onComplete: OnHeaderComplete);
        var headerDecoded = headerStreamText.DecodeText(headerLineBuilder, Text, token);
        _ = Task.Run(async () => {
            using (client) {
                var exception = client.Try();
                if (token.IsCancellationRequested) {
                    exception = new OperationCanceledException(token);
                }
                LogTransferResult(transfer, uri, exception);
                try {
                    headerStream.Complete(exception);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
                try {
                    await headerDecoded.ConfigureAwait(false);
                }
                catch (Exception ex) {
                    if (Log.Debug()) {
                        Log.Debug(ex);
                    }
                }
                try {
                    writeStream.Complete(exception);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
        }, CancellationToken.None);
        return writeStream;
    }
}
