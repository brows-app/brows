using Domore.Conf.Cli;

namespace Brows.Commands;

/// <summary>
/// Holds the parameters of the export command.
/// </summary>
internal sealed record ExportParameter {
    /// <summary>
    /// Gets or sets the file or directory that receives the messages.
    /// </summary>
    [CliArgument]
    [CliRequired]
    public string Destination {
        get => field ?? string.Empty;
        set;
    }

    /// <summary>
    /// Gets or sets whether an existing destination is replaced.
    /// </summary>
    public bool Overwrite { get; set; }
}
