using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Brows.Gui {
    internal class CommanderController : TriggerController<ICommanderController>, ICommanderController {
        private Window Window;
        private object WindowHandle;

        private void Window_Closed(object sender, EventArgs e) {
            WindowClosed?.Invoke(this, e);
        }

        protected override void OnLoaded(EventArgs e) {
            Window = Window.GetWindow(UserControl);
            Window.Closed += Window_Closed;
            WindowHandle = new WindowInteropHelper(Window).Handle;
            base.OnLoaded(e);
        }

        protected override void OnUnloaded(EventArgs e) {
            var window = Window;
            if (window != null) {
                window.Closed -= Window_Closed;
            }
            WindowHandle = null;
            base.OnUnloaded(e);
        }

        public event EventHandler WindowClosed;

        public void CloseWindow() {
            var window = Window;
            if (window != null) window.Close();
        }

        public object NativeWindow() {
            return WindowHandle;
        }

        public CommanderController(UserControl userControl) : base(userControl) {
        }
    }
}
