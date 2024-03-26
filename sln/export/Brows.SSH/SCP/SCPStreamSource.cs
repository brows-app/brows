using Brows.SSH;
using Brows.SSH.Native;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SCP {
    internal sealed class SCPStreamSource : EntryStreamSource<SSHEntry> {
        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            if (Entry.Info.Kind == SSHEntryKind.File) {
                var path = Entry.Info.Path;
                var cancel = BrowsCanceler.From(token);
                var scpRecv = await Client.SCPRecv(path, token);
                var scpRecvReady = Task.Run(cancellationToken: token, action: () => {
                    scpRecv.Ready(cancel);
                });
                await scpRecvReady;
                Stream = scpRecv.Stream();
                return new Disposable(scpRecv);
            }
            return await base.StreamReady(token);
        }

        public sealed override string RelativePath =>
            Uri.UnescapeDataString(
                Entry.Uri.AbsolutePath
                    .Substring(Entry.Provider.Uri.AbsolutePath.Length)
                    .TrimStart('/'));

        public sealed override string SourceDirectory =>
            Entry.Info.Kind == SSHEntryKind.Directory
                ? RelativePath
                : null;

        public sealed override long StreamLength =>
            Entry.Info.Length ?? 0;

        public SSHClient Client { get; }

        public SCPStreamSource(SSHEntry entry, SSHClient client) : base(entry) {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public sealed override string ToString() {
            return Entry.ID;
        }

        private sealed class Disposable : EntryStreamReady {
            protected sealed override void Dispose(bool disposing) {
                if (disposing) {
                    ScpRecv.Dispose();
                }
                base.Dispose(disposing);
            }

            public ScpRecv ScpRecv { get; }

            public Disposable(ScpRecv scpRecv) {
                ScpRecv = scpRecv ?? throw new ArgumentNullException(nameof(scpRecv));
            }
        }
    }
}
