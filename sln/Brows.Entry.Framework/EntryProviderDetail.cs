using System;

namespace Brows {
    public abstract class EntryProviderDetail {
        protected internal virtual EntryProviderDetail For(EntryProvider provider) {
            if (provider == null) {
                return null;
            }
            if (provider.GetType() != Provider?.GetType()) {
                return null;
            }
            var type = GetType();
            var inst = Activator.CreateInstance(type);
            var detail = (EntryProviderDetail)inst;
            detail.Provider = provider;
            return detail;
        }

        public EntryProvider Provider { get; set; }
    }
}
