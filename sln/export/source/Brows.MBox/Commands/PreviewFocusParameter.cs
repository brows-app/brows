using Domore.Conf.Cli;

namespace Brows.Commands;

/// <summary>
/// Holds the parameters of the preview focus command.
/// </summary>
internal sealed record PreviewFocusParameter {
    /// <summary>
    /// Gets or sets the part of the panel that receives the focus.
    /// </summary>
    [CliArgument]
    public PreviewFocusTarget Target { get; set; } = PreviewFocusTarget.Preview;
}
