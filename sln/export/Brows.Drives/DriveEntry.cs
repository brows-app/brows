using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveEntry : Entry<DriveProvider> {
        private static readonly ILog Log = Logging.For(typeof(DriveEntry));

        private bool Refreshing;

        public Task RefreshComplete { get; set; } = Task.CompletedTask;

        public sealed override string ID => Info.Name;
        public sealed override string Name => Info.Name;
        public new DriveProvider Provider => base.Provider;

        public DriveInfo Info { get; }
        public DriveType Kind { get; }

        public DriveEntry(DriveProvider provider, DriveInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Kind = Info.DriveType;
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
    }
}
