using System;
using System.Windows.Controls;

namespace Brows.Gui {
    using Windows.Controls;

    internal class LogbookController : Controller<ILogbookController>, ILogbookController {
        private ScrollViewer ScrollViewer =>
            _ScrollViewer ?? (
            _ScrollViewer = UserControl.ListView.FindVisualChild<ScrollViewer>());
        private ScrollViewer _ScrollViewer;

        public new LogbookControl UserControl { get; }

        public LogbookController(LogbookControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }

        public double ScrollTo(double offset) {
            ScrollViewer.ScrollToVerticalOffset(offset);
            return ScrollViewer.VerticalOffset;
        }

        public double ScrollDown() {
            ScrollViewer.LineDown();
            return ScrollViewer.VerticalOffset;
        }

        public double ScrollUp() {
            ScrollViewer.LineUp();
            return ScrollViewer.VerticalOffset;
        }

        public double PageDown() {
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset + ScrollViewer.ViewportHeight);
            return ScrollViewer.VerticalOffset;
        }

        public double PageUp() {
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - ScrollViewer.ViewportHeight);
            return ScrollViewer.VerticalOffset;
        }
    }
}
