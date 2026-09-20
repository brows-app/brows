using Brows.Commands;
using Brows.Operations;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Panels;

public sealed class TestCommandContext : ICommandContext {
    /// <summary>
    /// The panels of the context.
    /// </summary>
    public TestPanelCollection Panels { get; set; } = new();

    /// <summary>
    /// The commands considered triggered by the context.
    /// </summary>
    public ICollection<ICommand> Triggered { get; } = [];

    /// <summary>
    /// The source of the context.
    /// </summary>
    public ICommandSource Source { get; set; }

    /// <summary>
    /// The line of the context.
    /// </summary>
    public ICommandLine Line { get; set; }

    /// <summary>
    /// The progress given to the task of <see cref="ICommandContext.Operate"/>.
    /// </summary>
    public IOperationProgress Progress { get; set; }

    /// <summary>
    /// The token given to the task of <see cref="ICommandContext.Operate"/>.
    /// </summary>
    public CancellationToken Token { get; set; }

    /// <summary>
    /// The palette configs of the calls to <see cref="ICommandContext.ShowPalette"/>.
    /// </summary>
    public ICollection<ICommandPaletteConfig> ShownPalette { get; } = [];

    public ICommander Commander { get; set; }
    public ICommanderDomain Domain { get; set; }
    public ICommandPalette Palette { get; set; }
    public IGesture Gesture { get; set; }
    public ICommandContextConf Conf { get; set; }
    public ICommandContextData Data { get; set; }
    public ICommandContextFlag Flag { get; set; }
    public ICommandContextHint Hint { get; set; }

    /// <summary>
    /// The input of the context. Defaults to the input of <see cref="Line"/>.
    /// </summary>
    public string Input {
        get => field ?? (Line?.HasInput(out var input) == true ? input.Trim() : "");
        set;
    }

    bool ICommandContext.DidTrigger(ICommand command) {
        return Triggered.Contains(command);
    }

    bool ICommandContext.DidTrigger(out IReadOnlySet<ICommand> commands) {
        commands = Triggered.ToHashSet();
        return commands.Count > 0;
    }

    bool ICommandContext.MayTrigger(ICommand command) {
        return Triggered.Contains(command);
    }

    bool ICommandContext.HasCommander(out ICommander commander) {
        commander = Commander;
        return commander != null;
    }

    bool ICommandContext.HasConf(out ICommandContextConf conf) {
        conf = Conf;
        return conf != null;
    }

    bool ICommandContext.HasData(out ICommandContextData data) {
        data = Data;
        return data != null;
    }

    bool ICommandContext.HasDomain(out ICommanderDomain domain) {
        domain = Domain;
        return domain != null;
    }

    bool ICommandContext.HasFlag(out ICommandContextFlag flag) {
        flag = Flag;
        return flag != null;
    }

    bool ICommandContext.HasGesture(out IGesture gesture) {
        gesture = Gesture;
        return gesture != null;
    }

    bool ICommandContext.HasInput(out string value) {
        value = Input;
        return value != "";
    }

    bool ICommandContext.HasLine(out ICommandLine line) {
        line = Line;
        return line != null;
    }

    bool ICommandContext.HasPalette(out ICommandPalette palette) {
        palette = Palette;
        return palette != null;
    }

    bool ICommandContext.HasPanel(out IPanel active) {
        active = Panels?.Active;
        return active != null;
    }

    bool ICommandContext.HasPanel(int column, out IPanel panel) {
        var self = (ICommandContext)this;
        if (self.HasPanels(out var panels)) {
            if (panels.HasColumn(column, out panel)) {
                return true;
            }
        }
        panel = null;
        return false;
    }

    bool ICommandContext.HasPanels(out IPanelCollection collection) {
        collection = Panels;
        return collection != null && collection.Count > 0;
    }

    bool ICommandContext.HasPanels(PanelPassiveMode mode, out IPanel active, out IPanel passive) {
        var self = (ICommandContext)this;
        if (self.HasPanel(out active) == false) {
            passive = null;
            return false;
        }
        var column = active.Column;
        switch (mode) {
            case PanelPassiveMode.None:
                column = -1;
                break;
            case PanelPassiveMode.Auto:
                if (self.HasPanels(out var collection) == false) {
                    passive = null;
                    return false;
                }
                switch (collection.Count) {
                    case 1:
                        break;
                    case 2:
                        column = active.Column == 0
                            ? 1
                            : active.Column == 1
                                ? 0
                                : -1;
                        break;
                    default:
                        column = -1;
                        break;
                }
                break;
            case PanelPassiveMode.Next:
                column++;
                break;
            case PanelPassiveMode.Previous:
                column--;
                break;
        }
        return self.HasPanel(column, out passive);
    }

    bool ICommandContext.HasSource(out ICommandSource source) {
        source = Source;
        return source != null;
    }

    bool ICommandContext.HasSource<T>(out T item, out IReadOnlyList<T> items) {
        if (Source?.Item is not T t) {
            item = default;
            items = default;
            return false;
        }
        item = t;
        items = Source?.Items?.OfType<T>()?.ToList();
        return items?.Count > 0;
    }

    bool ICommandContext.Operate(Func<IOperationProgress, CancellationToken, Task<bool>> task) {
        if (task == null) {
            return false;
        }
        return task(Progress, Token).GetAwaiter().GetResult();
    }

    void ICommandContext.SetConf(ICommandContextConf conf) {
        Conf = conf;
    }

    void ICommandContext.SetData(ICommandContextData data) {
        Data = data;
    }

    void ICommandContext.SetFlag(ICommandContextFlag flag) {
        Flag = flag;
    }

    void ICommandContext.SetHint(ICommandContextHint hint) {
        Hint = hint;
    }

    bool ICommandContext.ShowPalette(ICommandPaletteConfig config) {
        ShownPalette.Add(config);
        return true;
    }
}
