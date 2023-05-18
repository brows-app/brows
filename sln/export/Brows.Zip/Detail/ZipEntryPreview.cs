namespace Brows.Detail {
    internal sealed class ZipEntryPreview : ProviderDetail {
        protected override ProviderDetail Clone(Provider provider) {
            return provider is ZipProvider p
                ? new ZipEntryPreview(p)
                : null;
        }

        public ZipEntryPreview(ZipProvider provider) : base(provider) {
        }
    }
}
