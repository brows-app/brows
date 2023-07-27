using Brows.SSH;
using Domore.Logs;
using System;

namespace Brows {
    internal sealed class SSHEntry : Entry<SSHProvider> {
        private static readonly ILog Log = Logging.For(typeof(SSHEntry));

        public sealed override string ID { get; }
        public sealed override string Name { get; }

        public SSHEntryInfo Info { get; }
        public new SSHProvider Provider =>
            base.Provider;

        public SSHEntry(SSHProvider provider, SSHEntryInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Name = Info.Name;
            ID = string.Join('/', Provider.ID.TrimEnd('/'), Info.Name);
        }
    }
}
