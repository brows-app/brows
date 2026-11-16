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
internal sealed class SecretConfirmPasswordControl
: ModelView<SecretConfirmPassword, ISecretConfirmPasswordController>,
  ISecretConfirmPasswordController,
  IControllingGesture,
  IControllingText {
    private static readonly ILog Log = Logging.For(typeof(SecretConfirmPasswordControl));

    private PasswordBox GetPasswordBox() {
        return GetTemplateChild("PART_PasswordBox") as PasswordBox;
    }

    private PasswordBox GetConfirmBox() {
        return GetTemplateChild("PART_ConfirmBox") as PasswordBox;
    }

    SecureString ISecretConfirmPasswordController.SecretPassword {
        get {
            var passwordBox = GetPasswordBox();
            if (passwordBox is not null) {
                return passwordBox.SecurePassword;
            }
            return null;
        }
    }

    SecureString ISecretConfirmPasswordController.SecretConfirmPassword {
        get {
            var confirmBox = GetConfirmBox();
            if (confirmBox is not null) {
                return confirmBox.SecurePassword;
            }
            return null;
        }
    }

    void IPanelSecretController.Clear() {
        var passwordBox = GetPasswordBox();
        if (passwordBox is not null) {
            passwordBox.Clear();
        }
        var confirmBox = GetConfirmBox();
        if (confirmBox is not null) {
            confirmBox.Clear();
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
