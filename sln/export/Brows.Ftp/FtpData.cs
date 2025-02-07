using Brows.Url;
using Brows.Url.Ftp;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FtpData : Notifier {
        private readonly Collection<FtpHeader> HeadersCollection = new();
        private readonly Collection<FtpListing> ListingCollection = new();

        public object Headers => HeadersCollection;
        public object Listing => ListingCollection;

        public async Task Read(FtpClient client, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(client);
            ListingCollection.Clear();
            HeadersCollection.Clear();
            var sw = Stopwatch.StartNew();
            var body = Channel.CreateUnbounded<UrlClientTextEventArgs>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            var header = Channel.CreateUnbounded<UrlClientTextEventArgs>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            void client_header(object sender, UrlClientTextEventArgs e) {
                header.Writer.TryWrite(e);
            }
            void client_listing(object sender, UrlClientTextEventArgs e) {
                body.Writer.TryWrite(e);
            }
            client.Header += client_header;
            client.Listing += client_listing;
            try {
                await HeadersCollection.Read(header.Reader.ReadAllAsync(token), (time, text) => new FtpHeader(sw.Elapsed, text), () => header.Writer.TryComplete(), token);
                await ListingCollection.Read(body.Reader.ReadAllAsync(token), (time, text) => new FtpListing(sw.Elapsed, text), () => body.Writer.TryComplete(), token);
            }
            finally {
                client.Header -= client_header;
                client.Listing -= client_listing;
            }
        }

        private sealed class Collection<TData> : Notifier {
            private static readonly PropertyChangedEventArgs CountProperty = new(nameof(Count));

            private readonly ObservableCollection<TData> Agent = [];

            public object Source => Agent;
            public int Count => Agent.Count;

            public void Clear() {
                Agent.Clear();
                NotifyPropertyChanged(CountProperty);
            }

            public async Task Read(IAsyncEnumerable<UrlClientTextEventArgs> collection, Func<DateTime, string, TData> factory, Action complete, CancellationToken token) {
                ArgumentNullException.ThrowIfNull(collection);
                ArgumentNullException.ThrowIfNull(factory);
                ArgumentNullException.ThrowIfNull(complete);
                Agent.Clear();
                NotifyPropertyChanged(CountProperty);
                await foreach (var item in collection.WithCancellation(token)) {
                    if (item is null) continue;
                    if (item.Clear) {
                        Agent.Clear();
                        NotifyPropertyChanged(CountProperty);
                    }
                    if (item.Complete) {
                        complete();
                    }
                    var text = item.Text;
                    if (text != null) {
                        Agent.Add(factory(item.Time, text));
                        NotifyPropertyChanged(CountProperty);
                    }
                }
            }
        }
    }
}
