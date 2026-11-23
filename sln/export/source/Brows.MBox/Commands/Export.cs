using Domore.Conf.Cli;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

/// <summary>
/// Exports the selected messages of a mailbox to the provider of a panel.
/// </summary>
internal sealed class Export : MBoxCommand<Export.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        var where = parameter.Where ?? WorkIOWhere.Auto;
        var panel = where switch {
            WorkIOWhere.Auto => PanelPassiveMode.Auto,
            WorkIOWhere.Active => PanelPassiveMode.Active,
            WorkIOWhere.Next => PanelPassiveMode.Next,
            WorkIOWhere.Previous => PanelPassiveMode.Previous,
            _ => PanelPassiveMode.None
        };
        if (!context.HasPanels(panel, out var active, out var passive)) {
            return false;
        }
        if (!active.HasMBoxSelection(out var entries)) {
            return false;
        }
        if (!passive.HasProviderService(out var target, out ICopyProvidedIO copy)) {
            /*
             * The provider of the panel cannot receive files, such as the mailbox itself.
             */
            return false;
        }
        var mode = parameter.Mode ?? Mode.Archive;
        var archive = mode == Mode.Archive;
        var set = new MBoxExportStreamSet(entries.OrderBy(entry => entry.Record.Offset), archive);
        return context.Operate(async (progress, token) => {
            var io = new List<IProvidedIO> {
                new ProvidedIO { StreamSets = new[] { set } }
            };
            return await copy.Work(io, target, progress, token);
        });
    }

    public enum Mode {
        Archive,
        EML,
    }

    /// <summary>
    /// Holds the parameters of the export command.
    /// </summary>
    public sealed record Parameter {
        /// <summary>
        /// Gets or sets how the panel that receives the messages is chosen.
        /// </summary>
        [CliArgument]
        public WorkIOWhere? Where { get; set; }

        /// <summary>
        /// Gets or sets what is exported for each message: the message, or an archive of the message and its
        /// attachments.
        /// </summary>
        [CliArgument]
        public Mode? Mode { get; set; }
    }
}
