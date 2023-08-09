using System.Collections.Generic;

namespace Brows.Gui {
    public interface IPanelCollectionController {
        void AddPanel(IPanel panel);
        void AddPanels(IEnumerable<IPanel> panels);
        void RemovePanel(IPanel panel);
        void RemovePanels(IEnumerable<IPanel> panels);
    }
}
