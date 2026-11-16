using System.Security;

namespace Brows.Panels.PanelSecrets;

internal interface ISecretConfirmPasswordController : IPanelSecretController {
    SecureString SecretPassword { get; }
    SecureString SecretConfirmPassword { get; }
}
