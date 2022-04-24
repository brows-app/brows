using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    public abstract class ImageSourceProvider : IImageSourceProvider {
    }

    public abstract class ImageSourceProvider<TInput> : ImageSourceProvider, IImageSourceProvider<TInput> {
        public abstract Task<object> GetImageSource(TInput input, ImageSize size, CancellationToken cancellationToken);
    }
}
