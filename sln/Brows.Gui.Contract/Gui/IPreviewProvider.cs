using System.Collections.Generic;
using System.Threading;

namespace Brows.Gui {
    public interface IPreviewProvider : IImageSourceProvider<IPreviewInput> {
        IAsyncEnumerable<string> GetPreviewText(IPreviewInput input, CancellationToken cancellationToken);
    }
}
