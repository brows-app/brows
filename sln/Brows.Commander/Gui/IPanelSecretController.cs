using System.Security;

namespace Brows.Gui {
    public interface IPanelSecretController : IController {
        SecureString SecretString { get; }
        void Clear();
        void Focus();
    }
}
