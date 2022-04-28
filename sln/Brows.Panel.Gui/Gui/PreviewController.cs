using System;
using System.Windows;

namespace Brows.Gui {
    internal class PreviewController : Controller<IPreviewController>, IPreviewController {
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            SizeChanged?.Invoke(this, e);
        }

        public event EventHandler SizeChanged;

        public double Width => UserControl.ActualWidth;
        public double Height => UserControl.ActualHeight;

        public new PreviewControl UserControl { get; }

        public PreviewController(PreviewControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.SizeChanged += UserControl_SizeChanged;
        }
    }
}
