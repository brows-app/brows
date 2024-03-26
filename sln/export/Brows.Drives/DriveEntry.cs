using Brows.FileSystem;
using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveEntry : Entry<DriveProvider>, IFileSystemInfo {
        private static readonly ILog Log = Logging.For(typeof(DriveEntry));

        private bool Refreshing;

        public Task RefreshComplete { get; set; } = Task.CompletedTask;

        public sealed override string ID { get; }
        public sealed override string Name { get; }
        public new DriveProvider Provider => base.Provider;

        public char? Char => _Char ??=
            Name.Length > 0 ? Name.ToUpperInvariant()[0] : default;
        private char? _Char;

        public DriveInfo Info { get; }
        public DriveType Kind { get; }

        public DriveEntry(DriveProvider provider, DriveInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Kind = Info.DriveType;
            Name = ID = Info.Name;
        }

        public async Task Refresh(CancellationToken token) {
            if (RefreshComplete == null) {
                RefreshComplete = Task.Run(cancellationToken: token, action: () => {
                    if (Log.Debug()) {
                        Log.Debug(nameof(Refreshing) + " > " + Info);
                    }
                    Refreshing = true;
                    try {
                    }
                    finally {
                        Refreshing = false;
                    }
                });
            }
            await RefreshComplete;
        }

        FileSystemInfo IFileSystemInfo.Info => Info.IsReady
            ? Info.RootDirectory
            : null;
    }
}
