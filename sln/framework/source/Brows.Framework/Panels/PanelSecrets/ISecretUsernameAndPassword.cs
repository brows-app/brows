using System.Security;

namespace Brows.Panels.PanelSecrets;

public interface ISecretUsernameAndPassword : IPanelSecret {
    string Username { get; }
    SecureString Password { get; }
}
