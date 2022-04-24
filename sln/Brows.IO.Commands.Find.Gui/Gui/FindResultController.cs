using System;

namespace Brows.Gui {
    internal class FindResultController : CollectionController<IFindResultController, FindItem>, IFindResultController {
        public new FindResultControl UserControl { get; }

        public FindResultController(FindResultControl userControl) : base(userControl, userControl?.ListView) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }

        public void Up() {
            UserControl.ListView.MoveToPreviousOne();
        }

        public void Down() {
            UserControl.ListView.MoveToNextOne();
        }
    }
}
