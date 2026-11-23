using Brows.Composition;
using System;
using System.Security;
using System.Threading.Tasks;

namespace Brows.Security;

/// <summary>
/// Retrieves the additional secret used to protect data.
/// </summary>
public interface IGetEntropy : IExport {
    /// <summary>
    /// Retrieves the additional secret used for data protection.
    /// </summary>
    /// <param name="set">
    /// Receives the secret.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if a secret was retrieved; otherwise, false.
    /// </returns>
    Task<bool> Work(Action<SecureString> set, CancellationToken token);
}
