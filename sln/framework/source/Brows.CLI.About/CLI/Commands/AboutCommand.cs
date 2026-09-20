using Domore.Conf;
using Domore.Logs;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI.Commands;

internal sealed class AboutCommand : CliCommand<AboutCommand.Param> {
    private static readonly ILog Log = Logging.For(typeof(AboutCommand));

    protected sealed override async Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
        if (null == param) throw new ArgumentNullException(nameof(param));
        if (null == context) throw new ArgumentNullException(nameof(context));
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly is null
            ? null
            : FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        if (Log.Info()) {
            Log.Info(
                Out(context.ProgramName),
                Out($"{version}"),
                $"Use '{context.ProgramName} help' for more info");
        }
        await Task.CompletedTask;
        return version;
    }

    /// <summary>
    /// The parameter for <see cref="AboutCommand"/>.
    /// </summary>
    [ConfHelp(@"
        Displays information about the current program.")]
    public sealed class Param {
    }
}
