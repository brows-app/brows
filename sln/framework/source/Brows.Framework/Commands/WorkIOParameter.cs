using Brows.Panels;
using Domore.Conf.Cli;

namespace Brows.Commands;

/// <summary>
/// Specifies the destination panel for a transfer command.
/// </summary>
public class WorkIOParameter {
    internal PanelPassiveMode Panel() {
        switch (Where) {
            case WorkIOWhere.Auto:
                return PanelPassiveMode.Auto;
            case WorkIOWhere.Active:
                return PanelPassiveMode.Active;
            case WorkIOWhere.Next:
                return PanelPassiveMode.Next;
            case WorkIOWhere.None:
                return PanelPassiveMode.None;
            case WorkIOWhere.Previous:
                return PanelPassiveMode.Previous;
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Gets or sets how the destination panel is chosen.
    /// </summary>
    [CliArgument]
    [CliRequired]
    public WorkIOWhere Where { get; set; }
}
