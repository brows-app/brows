using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Brows {
    using Gui;

    public abstract class DriveEntry : Entry {
        private IReadOnlyDictionary<string, IEntryData> Data =>
            _Data ?? (
            _Data = Wrap.Data());
        private IReadOnlyDictionary<string, IEntryData> _Data;

        private DriveInfoWrapper Wrap =>
            _Wrap ?? (
            _Wrap = DriveInfoWrapper.For(Info, CancellationToken));
        private DriveInfoWrapper _Wrap;

        protected override IReadOnlySet<string> Keys =>
            _Keys ?? (
            _Keys = new HashSet<string>(Data.Keys));
        private IReadOnlySet<string> _Keys;

        protected override IconInput IconInput =>
            _IconInput ?? (
            _IconInput = new IconInput(Info.GetIconStock()));
        private IconInput _IconInput;

        protected DriveEntry(DriveInfo info, CancellationToken cancellationToken) : base(cancellationToken) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }

        protected override IEntryData Get(string key) {
            return Data[key];
        }

        public override string ID =>
            Info.Name;

        public DriveInfo Info { get; }

        public object Ready(Func<DriveInfo, object> f = null) {
            var r = Info.IsReady;
            if (f == null) return r;
            if (r) return f(Info);
            return null;
        }
    }
}
