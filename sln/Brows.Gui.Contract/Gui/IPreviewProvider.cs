using Domore.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    public interface IPreviewProvider : IImageSourceProvider<IPreviewInput> {
        Task<DecodedText> GetPreviewText(IPreviewInput input, DecodedTextBuilder builder, CancellationToken cancellationToken);
    }
}
