using System.Collections.Generic;

namespace Brows {
    using Triggers;

    public interface ICommandContext {
        void SetHint(ICommandContextHint hint);
        void SetData(ICommandContextData data);
        bool HasData(out ICommandContextData data);
        void SetFlag(ICommandContextFlag flag);
        bool HasFlag(out ICommandContextFlag flag);
        bool CanBookmark(out IBookmark bookmark);
        bool HasClipboard(out IClipboard clipboard);
        bool HasCommander(out ICommander commander);
        bool HasInput(out string value);
        bool HasPanel(out IPanel active);
        bool HasPanel(int column, out IPanel panel);
        bool HasPanels(out IReadOnlyList<IPanel> collection);
        bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive);
        bool HasKey(out KeyboardGesture gesture);
        bool HasProvider(out IPanelProvider provider);
        bool HasInfo(out ICommandInfo info);
        bool HasTrigger(out ITrigger trigger);
        bool DidTrigger(ICommand command);
    }
}
