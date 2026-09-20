using Brows.Operations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

public delegate Task EntryStreamConsumingDelegate(IEntryStreamSource source, Stream stream, IOperationProgress progress, CancellationToken token);
