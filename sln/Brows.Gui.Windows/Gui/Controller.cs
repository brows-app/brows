using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Gui {
    public class Controller {
        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            OnLoaded(e);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) {
            OnUnloaded(e);
        }

        protected virtual void OnLoaded(EventArgs e) => Loaded?.Invoke(this, e);
        protected virtual void OnUnloaded(EventArgs e) => Unloaded?.Invoke(this, e);

        public event EventHandler Loaded;
        public event EventHandler Unloaded;

        public UserControl UserControl { get; }

        public Controller(UserControl userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.Loaded += UserControl_Loaded;
            UserControl.Unloaded += UserControl_Unloaded;
        }
    }

    public class Controller<TController> : Controller where TController : class {
        private void Control_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var oldValue = e.OldValue;
            if (oldValue is IControlled<TController> oldHave) {
                oldHave.Controller = null;
            }
            var loaded = UserControl.IsLoaded;
            if (loaded) {
                var newValue = e.NewValue;
                if (newValue is IControlled<TController> newHave) {
                    newHave.Controller = this as TController;
                }
            }
        }

        protected override void OnLoaded(EventArgs e) {
            var dataContext = UserControl.DataContext;
            if (dataContext is IControlled<TController> have) {
                have.Controller = this as TController;
            }
            base.OnLoaded(e);
        }

        protected override void OnUnloaded(EventArgs e) {
            var dataContext = UserControl.DataContext;
            if (dataContext is IControlled<TController> have) {
                have.Controller = null;
            }
            base.OnUnloaded(e);
        }

        public Controller(UserControl userControl) : base(userControl) {
            if (null == userControl) throw new ArgumentNullException(nameof(userControl));
            userControl.DataContextChanged += Control_DataContextChanged;
        }
    }
}
