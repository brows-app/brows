namespace Brows.Detail {
    internal sealed class FileSystemEntryPreview : ProviderDetail {
        protected override ProviderDetail Clone(Provider provider) {
            return provider is FileSystemProvider p
                ? new FileSystemEntryPreview(p)
                : null;
        }

        public FileSystemEntryPreview(FileSystemProvider provider) : base(provider) {
        }
    }
}
