using System.Threading;
using System.Threading.Tasks;

namespace Domore.Text {
    public delegate Task DecodedTextDelegate(DecodedText decode, CancellationToken cancellationToken);
}
