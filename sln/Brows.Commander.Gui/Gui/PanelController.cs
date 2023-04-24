using System;
using System.Windows;
using System.Windows.Input;

namespace Brows.Gui {
    internal class PanelController : Controller<IPanelController>, IPanelController {
        private void UserControl_Drop(object sender, DragEventArgs e) {
            Dropped = new PanelDrop(e);
            Drop?.Invoke(this, e);
            Dropped = null;
        }

        private void UserControl_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
        }

        private void UserControl_DragOver(object sender, DragEventArgs e) {
        }

        private void UserControl_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) {
        }

        private void UserControl_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) {
            FocusedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            SizeChanged?.Invoke(this, e);
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Activated?.Invoke(this, e);
        }

        public event EventHandler Drop;
        public event EventHandler FocusedChanged;
        public event EventHandler SizeChanged;
        public event EventHandler Activated;

        public bool Focused => Element.IsKeyboardFocusWithin;
        public double Width => Element.ActualWidth;
        public double Height => Element.ActualHeight;

        public new PanelControl Element { get; }

        public PanelController(PanelControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.AllowDrop = true;
            Element.Drop += UserControl_Drop;
            Element.DragOver += UserControl_DragOver;
            Element.GiveFeedback += UserControl_GiveFeedback;
            Element.IsKeyboardFocusWithinChanged += UserControl_IsKeyboardFocusWithinChanged;
            Element.PreviewMouseDown += UserControl_PreviewMouseDown;
            Element.QueryContinueDrag += UserControl_QueryContinueDrag;
            Element.SizeChanged += UserControl_SizeChanged;
        }

        IPanelDrop IPanelController.Dropped =>
            Dropped;
        private IPanelDrop Dropped;
    }
}
