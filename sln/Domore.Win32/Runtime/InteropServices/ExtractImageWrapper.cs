namespace Domore.Runtime.InteropServices {
    using ComTypes;

    public sealed class ExtractImageWrapper : AssocQueryStringWrapper<IExtractImage> {
        protected override string IID => ComTypes.IID.IExtractImage;

        public IExtractImage ExtractImage =>
            ComObject();

        public ExtractImageWrapper(string extension) : base(extension) {
        }
    }
}
