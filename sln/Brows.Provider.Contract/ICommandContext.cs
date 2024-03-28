using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommandContext {
        void SetConf(ICommandContextConf conf);
        bool HasConf(out ICommandContextConf conf);
        void SetHint(ICommandContextHint hint);
        void SetData(ICommandContextData data);
        bool HasData(out ICommandContextData data);
        void SetFlag(ICommandContextFlag flag);
        bool HasFlag(out ICommandContextFlag flag);
        bool HasPalette(out ICommandPalette palette);
        bool HasCommander(out ICommander commander);
        bool HasDomain(out ICommanderDomain domain);
        bool HasInput(out string value);
        bool HasPanel(out IPanel active);
        bool HasPanel(int column, out IPanel panel);
        bool HasPanels(out IPanelCollection collection);
        bool HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive);
        bool HasGesture(out IGesture gesture);
        bool HasLine(out ICommandLine line);
        bool MayTrigger(ICommand command);
        bool DidTrigger(ICommand command);
        bool DidTrigger(out IReadOnlySet<ICommand> commands);
        bool HasSource(out ICommandSource source);
        bool HasSource<T>(out T item, out IReadOnlyList<T> items);
        bool ShowPalette(ICommandPaletteConfig config);
        bool Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task);
    }
}
