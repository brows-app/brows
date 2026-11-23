using Brows.Composition;
using System;
using System.Threading.Tasks;

namespace Brows.Security;

/// <summary>
/// Protects bytes using an additional secret.
/// </summary>
public interface IProtectData : IExport {
    /// <summary>
    /// Protects data using the supplied additional secret.
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
