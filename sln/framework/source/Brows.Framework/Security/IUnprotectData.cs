using Brows.Composition;
using System;
using System.Threading.Tasks;

namespace Brows.Security;

/// <summary>
/// Restores protected bytes using the secret used to protect them.
/// </summary>
public interface IUnprotectData : IExport {
    /// <summary>
    /// Restores data using the supplied additional secret.
    /// </summary>
    /// <param name="data">
    /// The bytes to process.
    /// </param>
    /// <param name="entropy">
    /// The additional secret; restoration requires the value used for protection.
    /// </param>
    /// <param name="set">
    /// Receives the resulting bytes.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the operation succeeded; otherwise, false.
    /// </returns>
    Task<bool> Work(byte[] data, byte[] entropy, Action<byte[]> set, CancellationToken token);
}
