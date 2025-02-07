using Brows.Ftp;
using Brows.Url.Ftp;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FtpProvider : FileProtocolProvider<FtpEntry, FtpConfig> {
        private static readonly ILog Log = Logging.For(typeof(FtpProvider));

        private readonly FtpData Data = new();

        private FtpClient Client => _Client ??= (Factory.Client.Get(Uri, Config.Client.Values));
        private FtpClient _Client;

        protected sealed override async Task List(CancellationToken token) {
            var client = Client;
            var password = client.Credential?.Password;
            if (password == null) {
                var securePassword = await GetSecret("FtpProvider_Prompt_Password", new[] { client.Url }, token);
                if (securePassword != null) {
                    var protect = client.Credential?.ProtectPassword(securePassword, SecurityDataKind.Protected, token);
                    if (protect != null) {
                        await protect;
                    }
                }
            }
            ViewRaw = true;
            var read = Data.Read(client, token);
            var list = await client.List(token);
            await foreach (var info in list.Items) {
                await Provide(new FtpEntry(this, info), token);
                ViewRaw = false;
            }
            await read;
        }

        public bool ViewRaw {
            get => _ViewRaw.Value;
            set => Change(_ViewRaw, value);
        }
        private readonly Notified<bool> _ViewRaw = new(nameof(ViewRaw));

        public object Headers => Data.Headers;
        public object Listing => Data.Listing;

        public object Icon { get; }
        public FtpProviderFactory Factory { get; }

        public FtpProvider(FtpProviderFactory factory, Uri uri, object icon) : base(uri, 100, null, null) {
            Icon = icon;
            Factory = factory;
        }

        public FtpClient ClientFor(FtpEntry entry) {
            ArgumentNullException.ThrowIfNull(entry);
            return Factory.Client.Get(entry.Uri, Config.Client.Values);
        }

        private sealed class ProvideIO : ProvideIO<FtpProvider> {
            protected sealed override async IAsyncEnumerable<FtpEntry> List(FtpProvider provider, Uri uri, [EnumeratorCancellation] CancellationToken token) {
                ArgumentNullException.ThrowIfNull(provider);
                var relativeUri = $"{uri}".Substring($"{provider.Uri}".Length);
                var listClient = provider.Factory.Client.Get(uri, provider.Config.Client.Values);
                var list = await listClient.List(token);
                await foreach (var item in list.Items.WithCancellation(token)) {
                    yield return new FtpEntry(
                        provider,
                        item,
                        uri: FileProtocolUri.Extend(provider.Uri, [relativeUri, item.Name], includeTrailingSlash: item.EntryKind() == FileProtocolEntryKind.Directory));
                }
            }

            protected sealed override FileProtocolStreamSource<FtpEntry> Read(FtpProvider provider, FtpEntry entry) {
                return new FtpStreamSource(entry);
            }
        }

        private sealed class CopyProvidedIO : CopyProvidedIO<FtpProvider> {
            protected override Task CreateDirectory(FtpProvider provider, string path, CancellationToken token) {
                throw new NotImplementedException();
            }

            protected override Task<Stream> Write(FtpProvider provider, string path, long length, CancellationToken token) {
                throw new NotImplementedException();
            }
        }
    }
}
