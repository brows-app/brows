using System;

namespace Brows {
    public abstract class ProviderDetail {
        protected internal virtual ProviderDetail For(Provider provider) {
            if (provider == null) {
                return null;
            }
            if (provider.GetType() != Provider?.GetType()) {
                return null;
            }
            var type = GetType();
            var inst = Activator.CreateInstance(type);
            var detail = (ProviderDetail)inst;
            detail.Provider = provider;
            return detail;
        }

        public Provider Provider { get; set; }
    }
}
