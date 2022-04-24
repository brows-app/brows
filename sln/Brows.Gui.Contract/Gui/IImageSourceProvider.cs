using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    public interface IImageSourceProvider {
    }

    public interface IImageSourceProvider<TInput> : IImageSourceProvider {
        Task<object> GetImageSource(TInput input, ImageSize size, CancellationToken cancellationToken);
    }
}
