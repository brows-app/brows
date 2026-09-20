using Brows.CLI;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Brows.IPC.HostCommands;

/// <summary>
/// The 'call' command.
/// </summary>
public sealed class Call : HostCommand<Call.Param> {
    private static readonly ILog Log = Logging.For(typeof(Call));

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="param">The command parameter.</param>
    /// <param name="context">The command context.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes when the command finishes.</returns>
    protected sealed override async Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
        if (null == param) throw new ArgumentNullException(nameof(param));
        if (null == context) throw new ArgumentNullException(nameof(context));
        var d = param.GetDelegate();
        var ep = param.GetEndPoint();
        var host = new ObjectCallHost(d);
        var task = host.Run(token: token, info: new() {
            EndPoint = ep,
            OnStart = param.OnStart,
            OnFinish = param.OnFinish,
            /*
             * Wrap raw JSON in {"r":...} and {"w":...} envelopes in
             * the log output so reads and writes are distinguishable.
             */
            OnDataRead = (data, token) => {
                Log.Info(Out($"{{\"r\":{data}}}"));
                return Task.CompletedTask;
            },
            OnDataWrite = (data, token) => {
                Log.Info(Out($"{{\"w\":{data}}}"));
                return Task.CompletedTask;
            }
        });
        await task;
        return null;
    }

    /// <summary>
    /// The parameter for <see cref="Call"/>.
    /// </summary>
    public sealed class Param : HostCommandParam {
        internal IObjectCallDelegate GetDelegate() {
            IObjectCallDelegate core(out string name) {
                var n = name = Delegate?.Trim() ?? "";
                var factories = Factories?.Where(item => item is not null);
                if (factories is null) {
                    return null;
                }
                if (n == "") {
                    /*
                     * If no delegate name was provided, we take the first one available.
                     * This is useful when a program only declares a single delegate (and
                     * no others are imported). That single delegate will be the one created,
                     * here.
                     */
                    return factories
                        .Select(factory => factory.Create())
                        .FirstOrDefault(item => item is not null);
                }
                return factories
                    .Where(factory => n.Equals(factory.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(factory => factory.Create())
                    .FirstOrDefault(item => item is not null);
            }
            var obj = core(out var name);
            if (obj is null) {
                throw new CliCommandParamException(this, nameof(Delegate), $"The delegate '{name}' does not exist.");
            }
            return obj;
        }

        internal IPEndPoint GetEndPoint() {
            var endPoint = EndPoint?.Trim() ?? "";
            if (endPoint == "") {
                return null;
            }
            if (int.TryParse(endPoint, out var port)) {
                return new(IPAddress.Loopback, port);
            }
            throw new CliCommandParamException(this, nameof(EndPoint), $"The end point '{endPoint}' is invalid.");
        }

        /// <summary>
        /// Gets the command name.
        /// </summary>
        /// <returns>The command name.</returns>
        protected sealed override string GetCommandName() => nameof(Call);

        /// <summary>
        /// Gets the command parts.
        /// </summary>
        /// <returns>The command parts.</returns>
        protected sealed override string[] GetCommandParts() => [
            Delegate,
            $"{nameof(EndPoint)}={EndPoint}"
        ];

        /// <summary>
        /// Gets or sets the name of the delegate.
        /// </summary>
        [CliArgument]
        public string Delegate { get; set; }

        /// <summary>
        /// Gets or sets the end point.
        /// </summary>
        public string EndPoint { get; set; }

        /// <summary>
        /// Gets or sets the delegate factories available.
        /// </summary>
        [Conf(ignore: true)]
        [CliDisplay(include: false)]
        public IReadOnlyList<IObjectCallFactory> Factories { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked on start.
        /// </summary>
        [Conf(ignore: true)]
        [CliDisplay(include: false)]
        public Func<ObjectCallHostState, CancellationToken, Task> OnStart { get; init; }

        /// <summary>
        /// Gets or sets a callback invoked on finish.
        /// </summary>
        [Conf(ignore: true)]
        [CliDisplay(include: false)]
        public Func<ObjectCallHostState, CancellationToken, Task> OnFinish { get; init; }
    }
}
