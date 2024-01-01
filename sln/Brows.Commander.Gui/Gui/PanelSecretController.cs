using Domore.Logs;
using System;
using System.Security;
using System.Windows;
using System.Windows.Input;

namespace Brows.Gui {
    internal sealed class PanelSecretController : Controller<IPanelSecretController>, IPanelSecretController {
        private static readonly ILog Log = Logging.For(typeof(PanelSecretController));

        private void Control_Loaded(object sender, RoutedEventArgs e) {
        }

        private void Password_Loaded(object sender, RoutedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Password_Loaded));
            }
            PasswordFocus();
        }

        private void Password_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Password_IsVisibleChanged));
            }
            if (Control.Password.IsVisible) {
                PasswordFocus();
            }
        }

        private void PasswordFocus() {
            var focused = Control.Password.Focus();
            var element = Keyboard.Focus(Control.Password);
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(focused), focused),
                    Log.Join(nameof(element), element));
            }
        }

        public PanelSecretControl Control { get; }

        public PanelSecretController(PanelSecretControl control) : base(control) {
            Control = control ?? throw new ArgumentNullException(nameof(control));
            Control.Loaded += Control_Loaded;
            Control.Password.Loaded += Password_Loaded;
            Control.Password.IsVisibleChanged += Password_IsVisibleChanged;
        }

        SecureString IPanelSecretController.SecretString =>
            Control.Password.SecurePassword;

        void IPanelSecretController.Clear() {
            Control.Password.Clear();
        }

        void IPanelSecretController.Focus() {
            PasswordFocus();
        }
    }
}
