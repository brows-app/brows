using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public delegate Task EntryStreamConsumingDelegate(IEntryStreamSource source, Stream stream, IOperationProgress progress, CancellationToken token);
}
