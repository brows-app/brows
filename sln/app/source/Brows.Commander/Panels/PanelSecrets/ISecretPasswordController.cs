using System.Security;

namespace Brows.Panels.PanelSecrets;

internal interface ISecretPasswordController : IPanelSecretController {
    SecureString SecretPassword { get; }
}
