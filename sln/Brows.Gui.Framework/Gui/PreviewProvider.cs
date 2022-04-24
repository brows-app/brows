using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows.Gui {
    public abstract class PreviewProvider : ImageSourceProvider<IPreviewInput>, IPreviewProvider {
        private PreviewTextReader TextReader =>
            _TextReader ?? (
            _TextReader = new PreviewTextReader());
        private PreviewTextReader _TextReader;

        public virtual IAsyncEnumerable<string> GetPreviewText(IPreviewInput input, CancellationToken cancellationToken) {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return TextReader.GetPreviewText(input.File, cancellationToken);
        }
    }
}
