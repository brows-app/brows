using System;
using System.Windows.Controls;

namespace Brows.Gui {
    internal class PreviewController : Controller<IPreviewController>, IPreviewController {
        public event EventHandler SizeChanged;

        public double Width => UserControl.ActualWidth;
        public double Height => UserControl.ActualHeight;

        public PreviewController(UserControl userControl) : base(userControl) {
        }
    }
}
