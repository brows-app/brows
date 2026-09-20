using Brows.Gui;
using System.Security;

namespace Brows.Panels;

public interface IPanelSecretController : IController {
    SecureString SecretString { get; }
    void Clear();
    void Focus();
}
