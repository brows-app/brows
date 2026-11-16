using System.Security;

namespace Brows.Panels.PanelSecrets;

internal interface ISecretUsernameAndPasswordController : IPanelSecretController {
    string SecretUsername { get; }
    SecureString SecretPassword { get; }
}
