using Brows.CLI;
using Brows.IPC.Client;
using Brows.IPC.TxRx;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Brows.IPC.HostCommands;

/// <summary>
/// The 'post' command.
/// </summary>
public sealed class Post : HostCommand<Post.Param> {
    private static readonly ILog Log = Logging.For(typeof(Post));

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
        var txrxProvider = param.TxRxProvider ?? TxRxProxyProvider.From(d.TypeResolver, param.GetEndPoints());
        using (txrxProvider) {
            var agent = new ObjectPoster(d, txrxProvider, continueOnCapturedContext: false, token: token,
                txrxInfo: new() {
                    OnSerialized = (s, token) => {
                        Log.Info(Out(s));
                        return Task.CompletedTask;
                    },
                    OnDeserializing = (s, token) => {
                        Log.Info(Out(s));
                        return Task.CompletedTask;
                    },
                    OnError = (ex, token) => {
                        if (ex is not null) {
                            if (Log.Debug()) {
                                Log.Debug(ex);
                            }
                            else {
                                if (Log.Warn()) {
                                    Log.Warn(ex.Message);
                                }
                            }
                            if (ex is ClientStreamException) {
                                var innerEx = ex.InnerException;
                                if (innerEx is not null) {
                                    if (Log.Debug()) {
                                        Log.Debug(innerEx);
                                    }
                                    else {
                                        if (Log.Warn()) {
                                            Log.Warn(innerEx.Message);
                                        }
                                    }
                                }
                            }
                        }
                        return Task.CompletedTask;
                    }
                });
            await agent.Task;
        }
        return null;
    }

    /// <summary>
    /// The parameter for <see cref="Post"/>.
    /// </summary>
    public sealed class Param : HostCommandParam {
        internal IObjectPostDelegate GetDelegate() {
            IObjectPostDelegate core(out string name) {
                var n = name = Delegate?.Trim() ?? "";
                var factories = DelegateFactories?.Where(item => item is not null)?.ToList();
                if (factories == null || factories.Count == 0) {
                    return null;
                }
                if (n == "") {
                    return factories
                        .Select(factory => factory.Create())
                        .FirstOrDefault(item => item is not null);
                }
                return factories
                    .Where(factory => n.Equals(factory.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(factory => factory.Create())
                    .FirstOrDefault();
            }
            var obj = core(out var name);
            if (obj is null) {
                throw new CliCommandParamException(this, nameof(Delegate), $"The delegate '{name}' does not exist.");
            }
            return obj;
        }

        internal IEnumerable<IPEndPoint> GetEndPoints() {
            var endPoints = EndPoints?.Trim() ?? "";
            if (endPoints == "") {
                throw new CliCommandParamException(this, nameof(EndPoints), "No end points");
            }
            var strings = endPoints.Split(';').Select(s => s.Trim());
            foreach (var s in strings) {
                if (int.TryParse(s, out var port)) {
                    yield return new(IPAddress.Loopback, port);
                }
                else {
                    throw new CliCommandParamException(this, nameof(EndPoints), $"Invalid end point '{s}'");
                }
            }
        }

        /// <summary>
        /// Gets the command name.
        /// </summary>
        /// <returns>The command name.</returns>
        protected sealed override string GetCommandName() => nameof(Post);

        /// <summary>
        /// Gets the command parts.
        /// </summary>
        /// <returns>The command parts.</returns>
        protected sealed override string[] GetCommandParts() => [
            Delegate,
            $"{nameof(EndPoints)}={EndPoints}"
        ];

        /// <summary>
        /// Gets or sets the delegate to use.
        /// </summary>
        [CliArgument]
        public string Delegate { get; set; }

        /// <summary>
        /// Gets or sets the end points to use.
        /// </summary>
        public string EndPoints { get; set; }

        /// <summary>
        /// Gets or sets the delegate factories available.
        /// </summary>
        [Conf(ignore: true)]
        [CliDisplay(include: false)]
        public IReadOnlyList<IObjectPostFactory> DelegateFactories { get; set; }

        /// <summary>
        /// Gets or sets the TxRx provider to use.
        /// </summary>
        [Conf(ignore: true)]
        [CliDisplay(include: false)]
        public TxRxProxyProvider TxRxProvider { get; set; }
    }
}
