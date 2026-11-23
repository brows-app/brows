using Brows.Composition;
using System.Security;

namespace Brows.Security;

/// <summary>
/// Supplies the additional secret used to protect data.
/// </summary>
public interface ISetEntropy : IExport {
    /// <summary>
    /// Sets the additional secret used for data protection.
    /// </summary>
    /// <param name="entropy">
    /// The additional secret, or null to clear it.
    /// </param>
    void Work(SecureString entropy);
}
