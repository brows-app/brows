using System;

namespace Brows.Gui {
    using Windows.Controls;

    internal class LogbookController : Controller<ILogbookController>, ILogbookController {
        public new LogbookControl UserControl { get; }

        public LogbookController(LogbookControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }
    }
}
