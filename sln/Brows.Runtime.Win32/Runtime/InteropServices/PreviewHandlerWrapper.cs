namespace Brows.Runtime.InteropServices {
    using ComTypes;

    internal sealed class PreviewHandlerWrapper : AssocQueryStringWrapper<IPreviewHandler> {
        protected override string IID => ComTypes.IID.IPreviewHandler;

        public PreviewHandlerWrapper(string extension) : base(extension) {
        }
    }
}
