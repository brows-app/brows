using Brows.SSH;
using System;

namespace Brows {
    internal sealed class SSHEntry : Entry<SSHProvider> {
        private Uri GetUri() {
            var
            uri = new UriBuilder(Provider.Uri);
            uri.Path = Info.Path;
            return uri.Uri;
        }

        public Uri Uri => _Uri ??= GetUri();
        private Uri _Uri;

        public sealed override string ID => _ID ??= Uri.ToString();
        private string _ID;

        public sealed override string Name => _Name ??= Info.Name;
        private string _Name;

        public SSHFileInfo Info { get; }
        public new SSHProvider Provider =>
            base.Provider;

        public SSHEntry(SSHProvider provider, SSHFileInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}
