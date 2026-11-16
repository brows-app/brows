using Brows.Gui;
using Brows.Windows.Controls;
using Domore.Logs;
using System.Security;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows.Panels.PanelSecrets;

/*
 * The controlling interfaces keep keystrokes that are typed into the
 * secret input boxes from reaching the command triggers of the app.
 */
internal sealed class SecretPasswordControl
: ModelView<SecretPassword, ISecretPasswordController>,
  ISecretPasswordController,
  IControllingGesture,
  IControllingText {
    private static readonly ILog Log = Logging.For(typeof(SecretPasswordControl));

    private PasswordBox GetPasswordBox() {
        return GetTemplateChild("PART_PasswordBox") as PasswordBox;
    }

    SecureString ISecretPasswordController.SecretPassword {
        get {
            var passwordBox = GetPasswordBox();
            if (passwordBox is not null) {
                return passwordBox.SecurePassword;
            }
            return null;
        }
    }

    void IPanelSecretController.Clear() {
        var passwordBox = GetPasswordBox();
        if (passwordBox is not null) {
            passwordBox.Clear();
        }
    }

    void IPanelSecretController.Focus() {
        var passwordBox = GetPasswordBox();
        if (passwordBox is not null) {
            var passwordBoxFocus = passwordBox.Focus();
            var keyboardFocus = Keyboard.Focus(passwordBox);
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(passwordBoxFocus), passwordBoxFocus),
                    Log.Join(nameof(keyboardFocus), keyboardFocus?.GetType()?.Name));
            }
        }
    }
}
