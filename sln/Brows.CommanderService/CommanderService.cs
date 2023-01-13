using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Cli;
    using Composition.Hosting;
    using Threading.Tasks;

    public class CommanderService {
        private static readonly ILog Log = Logging.For(typeof(CommanderService));

        private readonly List<Commander> Commanders = new List<Commander>();

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<CommanderService>());
        private TaskHandler _TaskHandler;

        private Composer Composer =>
            _Composer ?? (
            _Composer = new Composer());
        private Composer _Composer;

        private Cli.ICommandLine CommandLine =>
            _CommandLine ?? (
            _CommandLine = new CommandLine());
        private Cli.ICommandLine _CommandLine;

        private CommanderMessenger Messenger =>
            _Messenger ?? (
            _Messenger = new CommanderMessenger());
        private CommanderMessenger _Messenger;

        private CommandCollection Commands =>
            _Commands ?? (
            _Commands = new CommandCollection(Composition.Commands));
        private CommandCollection _Commands;

        private EntryProviderFactoryCollection EntryProviderFactory =>
            _EntryProviderFactory ?? (
            _EntryProviderFactory = new EntryProviderFactoryCollection(Composition.EntryProviderFactories));
        private EntryProviderFactoryCollection _EntryProviderFactory;

        private void Commander_Closed(object sender, EventArgs e) {
            var
            commander = (Commander)sender;
            commander.Closed -= Commander_Closed;
            commander.Exited -= Commander_Exited;
            commander.Loaded -= Commander_Loaded;
            commander.Themed -= Commander_Themed;
            Commanders.Remove(commander);
        }

        private void Commander_Loaded(object sender, EventArgs e) {
        }

        private void Commander_Exited(object sender, EventArgs e) {
            OnExited(new CommanderExitedEventArgs());
        }

        private void Commander_Themed(object sender, EventArgs e) {
            var commander = (Commander)sender;
            OnThemed(new CommanderThemedEventArgs(commander.Theme));
        }

        private async Task<Commander> Commander(bool first, string[] load, CancellationToken cancellationToken) {
            await Commands.Init(cancellationToken);
            var commander = new Commander {
                Clipboard = Import.Clipboard,
                Commands = Commands,
                Dialog = new DialogState(Import.DialogFactory.Create()),
                EntryProviderFactory = EntryProviderFactory,
                First = first,
                Load = load
            };
            commander.Closed += Commander_Closed;
            commander.Exited += Commander_Exited;
            commander.Loaded += Commander_Loaded;
            commander.Themed += Commander_Themed;
            Commanders.Add(commander);
            return commander;
        }

        private void Load(Commander commander) {
            if (null == commander) throw new ArgumentNullException(nameof(commander));
            OnLoaded(new CommanderLoadedEventArgs(commander, commander.First));
        }

        private async Task Service() {
            Load(await Commander(first: true, load: null, CancellationToken.None));
            await foreach (var message in Messenger.Receive(default)) {
                Load(await Commander(first: false, load: new[] { message }, CancellationToken.None));
            }
        }

        protected virtual void OnExited(CommanderExitedEventArgs e) {
            Exited?.Invoke(this, e);
        }

        protected virtual void OnLoaded(CommanderLoadedEventArgs e) {
            Loaded?.Invoke(this, e);
        }

        protected virtual void OnThemed(CommanderThemedEventArgs e) {
            Themed?.Invoke(this, e);
        }

        public event CommanderExitedEventHandler Exited;
        public event CommanderLoadedEventHandler Loaded;
        public event CommanderThemedEventHandler Themed;

        public IEnumerable<IComponentResource> ComponentResources =>
            Composition.ComponentResources;

        public IProgramComposition Composition =>
            _Composition ?? (
            _Composition = Composer.ProgramComposition());
        private IProgramComposition _Composition;

        public CommanderService(IProgramComposition composition) {
            _Composition = composition;
        }

        public CommanderService() : this(null) {
        }

        public void Begin() {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            TaskHandler.Begin(Service);
        }

        public static async Task Post(string message, CancellationToken cancellationToken) {
            var messenger = new CommanderMessenger();
            await messenger.Send(message, cancellationToken);
        }

        public static class Import {
            public static IClipboard Clipboard { get; set; }
            public static IDialogManagerFactory DialogFactory { get; set; }
        }
    }
}
