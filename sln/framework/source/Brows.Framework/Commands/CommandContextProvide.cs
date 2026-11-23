using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows.Commands;

/// <summary>
/// Specifies where to open a provider from a command.
/// </summary>
public enum CommandContextProvide {
    /// <summary>
    /// Opens in the active panel, adding a panel or window if needed.
    /// </summary>
    [Conf("h", "here")]
    [CliDisplayOverride("(h)ere")]
    ActivePanel,

    /// <summary>
    /// Opens in a new panel, adding a window if needed.
    /// </summary>
    [Conf("p", "panel")]
    [CliDisplayOverride("(p)anel")]
    AddPanel,

    /// <summary>
    /// Opens in a new commander window.
    /// </summary>
    [Conf("w", "window")]
    [CliDisplayOverride("(w)indow")]
    AddCommander
}
