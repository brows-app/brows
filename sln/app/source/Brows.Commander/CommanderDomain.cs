using Brows.Commands;
using Brows.Composition;
using Brows.Config;
using Brows.IPC;
using Brows.IPC.Messages;
using Brows.IPC.MessageSubscriptions;
using Brows.IPC.TxRx;
using Brows.Messaging;
using Brows.Providers;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public sealed class CommanderDomain : ICommanderDomain {
    private static readonly ILog Log = Logging.For(typeof(CommanderDomain));
    private static readonly CommanderMessageTypeResolver MessageTypeResolver = new();

    private readonly Lock Locker = new();
    private readonly List<Commander> Commanders = [];
    private CancellationTokenSource TokenSource;

    private MessageSetFactory Messages => field ??=
        new MessageSetFactory(Import.List<IMessageFactory>());

    private IConfig<CommanderConfig> Config => field ??=
        Configure.File<CommanderConfig>();

    private CommandCollection Commands => field ??=
        new CommandCollection(Import.List<ICommand>());

    private ProviderFactorySet Providers => field ??=
        new ProviderFactorySet(Import.List<IProviderFactory>());

    private void Commander_Closed(object sender, EventArgs e) {
        var
        commander = (Commander)sender;
        commander.Closed -= Commander_Closed;
        commander.Loaded -= Commander_Loaded;
        Commanders.Remove(commander);
    }

    private void Commander_Loaded(object sender, EventArgs e) {
    }

    private Commander Commander(bool first, IReadOnlyList<string> load) {
        var commander = new Commander(Providers, Messages, Commands, this) {
            First = first,
            Load = load
        };
        commander.Closed += Commander_Closed;
        commander.Loaded += Commander_Loaded;
        Commanders.Add(commander);
        return commander;
    }

    private void Load(Commander commander) {
        ArgumentNullException.ThrowIfNull(commander);
        Loaded?.Invoke(this, new CommanderLoadedEventArgs(commander, commander.First));
    }

    private async Task Service(CancellationToken token) {
        await Commands
            .Init(token);
        Load(Commander(
            first: true,
            load: (await Config.Load(token)).LoadFirst));
        using (var sub = CommanderMessageSubscription.ToHost()) {
            await foreach (var message in sub.Messages(token)) {
                if (message is Browse browse) {
                    var load = browse.ID?.Trim() switch {
                        var id when !string.IsNullOrWhiteSpace(id) => [id],
                        _ => (await Config.Load(token)).LoadFirst
                    };
                    var nextCommander = Commander(
                        first: false,
                        load: load);
                    Load(nextCommander);
                }
            }
        }
    }

    public event CommanderEndedEventHandler Ended;
    public event CommanderLoadedEventHandler Loaded;

    public IImport Import { get; }

    public CommanderDomain(IImport import) {
        Import = import ?? throw new ArgumentNullException(nameof(import));
    }

    public async void Begin() {
        if (Log.Info()) {
            Log.Info(nameof(Begin));
        }
        lock (Locker) {
            if (TokenSource == null) {
                TokenSource = new();
            }
            else {
                throw new BeginException();
            }
        }
        using (TokenSource) {
            try {
                await Service(TokenSource.Token);
            }
            catch (OperationCanceledException canceled) when (TokenSource.Token.IsCancellationRequested) {
                if (Log.Info()) {
                    Log.Info(nameof(canceled));
                }
            }
            TokenSource = null;
        }
    }

    public void End() {
        TokenSource?.Cancel();
        Ended?.Invoke(this, new CommanderEndedEventArgs());
    }

    public static async Task Post(string id, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(Post), id);
        }
        var portFile = HostSubscription.PortPath;
        var portFileExists = File.Exists(portFile);
        if (portFileExists != true) {
            return;
        }
        var portText = await File.ReadAllTextAsync(portFile, token);
        var port = int.TryParse(portText, out var p) ? p : default(int?);
        if (port.HasValue != true) {
            return;
        }
        var poster = new ObjectPoster(
           token: token,
           continueOnCapturedContext: false,
           postDelegate: new BrowsePostDelegate(id),
           txrxProxyProvider: TxRxProxyProvider.From(MessageTypeResolver, [new(IPAddress.Loopback, port.Value)]),
           txrxInfo: null);
        await poster.Task;
    }

    async Task<bool> ICommanderDomain.AddCommander(IReadOnlyList<string> panels, CancellationToken token) {
        if (panels == null) {
            var config = await Config.Load(token);
            panels = config.LoadFirst;
        }
        var commander = Commander(first: false, load: panels);
        Load(commander);
        return true;
    }

    private sealed class BeginException : Exception {
    }

    private sealed class BrowsePostDelegate : ObjectPostDelegate {
        public sealed override IObjectTypeResolver TypeResolver => MessageTypeResolver;

        private Browse Message => field ??= new() { ID = ID };

        public string ID { get; }

        public BrowsePostDelegate(string id) {
            ID = id;
        }

        protected sealed override Task<bool> Subscribe(CancellationToken token) {
            async void post() {
                var err = default(Exception);
                try {
                    await Post(Message, token);
                }
                catch (Exception ex) {
                    err = ex;
                }
                Complete(err);
            }
            post();
            return Task.FromResult(true);
        }
    }
}
