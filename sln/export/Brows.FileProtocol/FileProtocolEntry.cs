using System;

namespace Brows {
    public abstract class FileProtocolEntry : Entry {
        public abstract Uri Uri { get; }
        public abstract FileProtocolEntryKind Kind { get; }

        public string Extension => _Extension ??= new Func<string>(() => {
            var i = Name.LastIndexOf('.');
            if (i >= 0) {
                return Name.Substring(i);
            }
            return "";
        })();
        private string _Extension;

        public sealed override string ID => _ID ??= Uri.ToString();
        private string _ID;

        protected FileProtocolEntry(Provider provider) : base(provider) {
        }
    }

    public abstract class FileProtocolEntry<TProvider> : FileProtocolEntry where TProvider : Provider {
        protected new TProvider Provider { get; }

        protected FileProtocolEntry(TProvider provider) : base(provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }
}
