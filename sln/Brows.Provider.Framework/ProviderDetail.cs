namespace Brows {
    public abstract class ProviderDetail : IProviderFocus {
        protected abstract ProviderDetail Clone(Provider provider);

        protected ProviderDetail(Provider provider) {
            Provider = provider;
        }

        internal ProviderDetail For(Provider provider) {
            return Clone(provider);
        }

        public Provider Provider { get; }

        bool IProviderFocus.Set() {
            return false;
        }

        bool IProviderFocus.Get() {
            return false;
        }
    }
}
