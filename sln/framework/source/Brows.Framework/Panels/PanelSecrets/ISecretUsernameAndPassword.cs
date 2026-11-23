using System.Security;

namespace Brows.Panels.PanelSecrets;

/// <summary>
/// Provides a username and password requested by a panel.
/// </summary>
public interface ISecretUsernameAndPassword : IPanelSecret {
    /// <summary>
    /// Gets the username.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Gets the password.
    /// </summary>
    SecureString Password { get; }
}
