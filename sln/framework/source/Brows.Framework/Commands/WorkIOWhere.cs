using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows.Commands;

/// <summary>
/// Specifies the destination panel for a copy or move command.
/// </summary>
public enum WorkIOWhere {
    /// <summary>
    /// Does not choose a destination panel.
    /// </summary>
    [CliDisplay(false)]
    None = 0,

    /// <summary>
    /// Uses the active panel when it is the only panel, or the other panel when there are exactly two panels.
    /// With more panels, no destination is chosen.
    /// </summary>
    [Conf("a", "auto")]
    [CliDisplay(true)]
    [CliDisplayOverride("(a)uto")]
    Auto,

    /// <summary>
    /// Uses the active panel as the destination.
    /// </summary>
    [Conf("h", "here")]
    [CliDisplay(true)]
    [CliDisplayOverride("(h)ere")]
    Active,

    /// <summary>
    /// Uses the column immediately after the active panel, if present.
    /// </summary>
    [Conf("n", "next")]
    [CliDisplay(true)]
    [CliDisplayOverride("(n)ext")]
    Next,

    /// <summary>
    /// Uses the column immediately before the active panel, if present.
    /// </summary>
    [Conf("p", "previous")]
    [CliDisplay(true)]
    [CliDisplayOverride("(p)revious")]
    Previous
}
