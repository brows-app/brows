namespace Brows.Runtime.InteropServices {
    using ComTypes;

    internal sealed class ExtractImageWrapper : AssocQueryStringWrapper<IExtractImage> {
        protected override string IID => ComTypes.IID.IExtractImage;

        public IExtractImage ExtractImage =>
            ComObject();

        public ExtractImageWrapper(string extension) : base(extension) {
        }
    }
}
