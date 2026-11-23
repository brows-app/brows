using System.Security;

namespace Brows.Panels.PanelSecrets;

/// <summary>
/// Provides a password requested by a panel.
/// </summary>
public interface ISecretPassword : IPanelSecret {
    /// <summary>
    /// Gets the password.
    /// </summary>
    SecureString Password { get; }
}
