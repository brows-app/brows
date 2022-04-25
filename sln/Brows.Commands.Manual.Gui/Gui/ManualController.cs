using System;
using System.Windows.Controls;

namespace Brows.Gui {
    internal class ManualController : Controller<IManualController>, IManualController {
        private ScrollViewer ScrollViewer =>
            _ScrollViewer ?? (
            _ScrollViewer = UserControl.ListView.FindVisualChild<ScrollViewer>());
        private ScrollViewer _ScrollViewer;

        public object KeyTarget =>
            UserControl.ListView;

        public new ManualControl UserControl { get; }

        public ManualController(ManualControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }
    }
}
