using System;
using System.Windows;

namespace Brows.Gui {
    internal class PanelController : Controller<IPanelController>, IPanelController {
        private void UserControl_Drop(object sender, DragEventArgs e) {
            Drop?.Invoke(this, new DropEventArgs(new DragPayload(e)));
        }

        private void UserControl_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) {
            FocusedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            SizeChanged?.Invoke(this, e);
        }

        public event DropEventHandler Drop;
        public event EventHandler FocusedChanged;
        public event EventHandler SizeChanged;

        public bool Focused => UserControl.IsKeyboardFocusWithin;
        public double Width => UserControl.ActualWidth;
        public double Height => UserControl.ActualHeight;

        public new PanelControl UserControl { get; }

        public PanelController(PanelControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.AllowDrop = true;
            UserControl.Drop += UserControl_Drop;
            UserControl.IsKeyboardFocusWithinChanged += UserControl_IsKeyboardFocusWithinChanged;
            UserControl.SizeChanged += UserControl_SizeChanged;
        }
    }
}
