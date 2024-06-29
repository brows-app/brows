using Brows.Config;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class CommanderDomain : ICommanderDomain {
        private static readonly ILog Log = Logging.For(typeof(CommanderDomain));

        private readonly object Locker = new();
        private readonly List<Commander> Commanders = [];
        private CancellationTokenSource TokenSource;

        private MessageSetFactory Messages => _Messages ??=
            new MessageSetFactory(Import.List<IMessageFactory>());
        private MessageSetFactory _Messages;

        private IConfig<CommanderConfig> Config => _Config ??=
            Configure.File<CommanderConfig>();
        private IConfig<CommanderConfig> _Config;

        private CommanderMessenger Messenger => _Messenger ??=
            new CommanderMessenger();
        private CommanderMessenger _Messenger;

        private CommandCollection Commands => _Commands ??=
            new CommandCollection(Import.List<ICommand>());
        private CommandCollection _Commands;

        private ProviderFactorySet Providers => _Providers ??=
            new ProviderFactorySet(Import.List<IProviderFactory>());
        private ProviderFactorySet _Providers;

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
                .Init(token)
                .ConfigureAwait(false);
            Load(Commander(
                first: true,
                load: (await Config.Load(token)).LoadFirst));
            await foreach (var message in Messenger.Receive(token).ConfigureAwait(false)) {
                var msg = message?.Trim() ?? "";
                var nextCommander = Commander(
                    first: false,
                    load: msg != ""
                        ? new[] { msg }
                        : (await Config.Load(token).ConfigureAwait(false)).LoadFirst);
                Load(nextCommander);
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
                    await Service(TokenSource.Token).ConfigureAwait(false);
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

        public static Task Post(string message, CancellationToken cancellationToken) {
            var messenger = new CommanderMessenger();
            return messenger.Send(message, cancellationToken);
        }

        async Task<bool> ICommanderDomain.AddCommander(IReadOnlyList<string> panels, CancellationToken token) {
            if (panels == null) {
                var config = await Config.Load(token).ConfigureAwait(false);
                panels = config.LoadFirst;
            }
            var commander = Commander(first: false, load: panels);
            Load(commander);
            return true;
        }

        private class BeginException : Exception {
        }
    }
}
