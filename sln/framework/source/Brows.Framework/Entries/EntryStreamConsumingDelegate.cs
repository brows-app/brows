using Brows.Operations;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Processes a prepared entry stream while its resources remain available.
/// </summary>
/// <param name="source">
/// The source describing the entry content.
/// </param>
/// <param name="stream">
/// The prepared stream, or null when the source has no readable stream. Do not retain it after the task completes.
/// </param>
/// <param name="progress">
/// Receives progress updates for the operation.
/// </param>
/// <param name="token">
/// A token used to cancel the operation.
/// </param>
/// <returns>
/// A task that completes when processing finishes.
/// </returns>
public delegate Task EntryStreamConsumingDelegate(IEntryStreamSource source,
                                                  Stream stream,
                                                  IOperationProgress progress,
                                                  CancellationToken token);
