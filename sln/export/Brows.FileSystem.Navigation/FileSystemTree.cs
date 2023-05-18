using Brows.FileSystem;

namespace Brows {
    public sealed class FileSystemTree : ProviderNavigation {
        private FileSystemTree(ProviderNavigationNode root) : base(root) {
        }

        protected sealed override ProviderNavigation For(Provider provider) {
            if (provider is IFileSystemNavigationProvider) {
                return new FileSystemTree(Root as FileSystemTreeNode);
            }
            return null;
        }

        public IFileSystemNavigationService Service { get; }

        public FileSystemTree(IFileSystemNavigationService service) : this(new FileSystemTreeNode(service)) {
            Service = service;
        }
    }
}
