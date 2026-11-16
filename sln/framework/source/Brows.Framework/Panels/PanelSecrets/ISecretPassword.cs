using System.Security;

namespace Brows.Panels.PanelSecrets;

public interface ISecretPassword : IPanelSecret {
    SecureString Password { get; }
}
