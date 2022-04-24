using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class FileSystemPanelID : PanelID {
        protected override bool HasCanonicalValue => true;

        protected override async Task<string> GetCanonicalValue(CancellationToken cancellationToken) {
            return await Info.GetCanonicalFullNameAsync(cancellationToken);
        }

        public DirectoryInfo Info { get; }

        public FileSystemPanelID(DirectoryInfo info) : base(info?.FullName) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}
