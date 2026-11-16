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
internal sealed class SecretUsernameAndPasswordControl
: ModelView<SecretUsernameAndPassword, ISecretUsernameAndPasswordController>,
  ISecretUsernameAndPasswordController,
  IControllingGesture,
  IControllingText {
    private static readonly ILog Log = Logging.For(typeof(SecretUsernameAndPasswordControl));

    private PasswordBox GetPasswordBox() {
        return GetTemplateChild("PART_PasswordBox") as PasswordBox;
    }

    private TextBox GetUsernameBox() {
        return GetTemplateChild("PART_UsernameBox") as TextBox;
    }

    string ISecretUsernameAndPasswordController.SecretUsername {
        get {
            var usernameBox = GetUsernameBox();
            if (usernameBox is not null) {
                return usernameBox.Text;
            }
            return null;
        }
    }

    SecureString ISecretUsernameAndPasswordController.SecretPassword {
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
        var usernameBox = GetUsernameBox();
        if (usernameBox is not null) {
            usernameBox.Clear();
        }
    }

    void IPanelSecretController.Focus() {
        var usernameBox = GetUsernameBox();
        if (usernameBox is not null) {
            var usernameBoxFocus = usernameBox.Focus();
            var keyboardFocus = Keyboard.Focus(usernameBox);
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(usernameBoxFocus), usernameBoxFocus),
                    Log.Join(nameof(keyboardFocus), keyboardFocus?.GetType()?.Name));
            }
        }
    }
}
