using Brows.IO;
using Brows.Url.Extensions;
using Domore.IO.Extensions;
using Domore.Logs;
using Domore.Text;
using Domore.Text.Builders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url {
    public abstract class UrlClient {
        private static readonly ILog Log = Logging.For(typeof(UrlClient));

        private void OnHeaderLine(string line) {
            if (Log.Info()) {
                Log.Info(line);
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

        private async Task<ClientForUrl> Create(CancellationToken token) {
            var uri = Uri;
            var resolve = Resolve;
            var resolvedUri = resolve
                ? await UrlHost.Resolve(uri, token).ConfigureAwait(false)
                : uri;
            if (Log.Info()) {
                Log.Info(resolvedUri.Equals(uri)
                    ? Log.Join("Using", uri)
                    : Log.Join("Resolved", uri, "to", resolvedUri));
            }
            var client = new ClientForUrl(resolvedUri);
            var credential = Credential;
            if (credential != null) {
                var username = credential.Username;
                if (username != null && username != "") {
                    if (Log.Info()) {
                        Log.Info(Log.Join("Setting username for", resolvedUri));
                    }
                    client.Username(username);
                }
                var password = credential.Password;
                if (password != null && password != "") {
                    if (Log.Info()) {
                        Log.Info(Log.Join("Setting password for", resolvedUri));
                    }
                    client.Password(await credential.UnprotectPassword(token).ConfigureAwait(false));
                }
            }
            Configure(client);
            return client;
        }

        private protected async Task<UrlListing<T>> List<T>(UrlListingParser<T> parser, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(parser);
            var stream = await Read(null, token).ConfigureAwait(false);
            var streamText = ClientStreamText.Create(stream);
            var streamTextBuilder = new TextStreamBuilder();
            var streamLineBuilder = new TextLineBuilder(
                onLine: OnListLine,
                onClear: OnListClear,
                onComplete: OnListComplete);
            var streamBuilders = new DecodedTextBuilder[] { streamTextBuilder, streamLineBuilder };
            var streamTextTask = streamText.DecodeText(streamBuilders, Text, token);
            async IAsyncEnumerable<string> contents() {
                await foreach (var item in streamTextBuilder.Read(token).ConfigureAwait(false)) {
                    if (item.Clear == false) {
                        yield return item.Text;
                    }
                }
            }
            async IAsyncEnumerable<T> list() {
                await foreach (var item in parser.ParseContents(contents(), token).ConfigureAwait(false)) {
                    yield return item;
                }
                await streamTextTask.ConfigureAwait(false);
            }
            return new UrlListing<T>(list());
        }

        internal object Clone() {
            return MemberwiseClone();
        }

        protected virtual void Configure(ClientForUrl client) {
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

        public async Task<Stream> Read(long? length, CancellationToken token) {
            var client = await Create(token).ConfigureAwait(false);
            var writeStream = ClientStream.Create(client, ClientStreamKind.Write, length ?? -1);
            var headerStream = ClientStream.Create(client, ClientStreamKind.Header, length ?? -1);
            var headerStreamText = ClientStreamText.Create(headerStream);
            var headerLineBuilder = new TextLineBuilder(
                onLine: OnHeaderLine,
                onClear: OnHeaderClear,
                onComplete: OnHeaderComplete);
            _ = headerStreamText.DecodeText(headerLineBuilder, Text, token);
            _ = Task.Run(cancellationToken: token, action: () => {
                using (client) {
                    var exception = client.Try();
                    try {
                        headerStream.Complete(exception);
                    }
                    catch (Exception ex) {
                        if (Log.Error()) {
                            Log.Error(ex);
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
            });
            return writeStream;
        }
    }
}
