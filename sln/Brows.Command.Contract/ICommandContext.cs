using System.Collections.Generic;

namespace Brows {
    public interface ICommandContext {
        void SetHint(ICommandContextHint hint);
        void SetData(ICommandContextData data);
        bool HasData(out ICommandContextData data);
        void SetFlag(ICommandContextFlag flag);
        bool HasFlag(out ICommandContextFlag flag);
        bool CanBookmark(out IBookmark bookmark);
        bool HasClipboard(out IClipboard clipboard);
        bool HasCommander(out ICommander commander);
        bool HasEntries(out IEntryCollection entries);
        bool HasInput(out string value);
        bool HasPanel(out IPanel active);
        bool HasPanel(int column, out IPanel panel);
        bool HasPanels(out IReadOnlyList<IPanel> collection);
        bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive);
        bool HasKey(out PressGesture gesture);
        bool HasProvider(out IPanelProvider provider);
        bool HasLine(out ICommandLine line);
        bool MayTrigger(ICommand command);
        bool DidTrigger(ICommand command);
    }
}
