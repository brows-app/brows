using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Cli;
    using ComponentModel.Composition.Hosting;
    using Logger;
    using Threading.Tasks;

    public class CommanderService {
        private readonly List<Commander> Commanders = new List<Commander>();

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(CommanderService)));
        private ILog _Log;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<CommanderService>());
        private TaskHandler _TaskHandler;

        private Composer Composer =>
            _Composer ?? (
            _Composer = new Composer());
        private Composer _Composer;

        private Composition Composition =>
            _Composition ?? (
            _Composition = Composer.Compose());
        private Composition _Composition;

        private ICommandLine CommandLine =>
            _CommandLine ?? (
            _CommandLine = new CommandLine());
        private ICommandLine _CommandLine;

        private ICommanderMessageQueue MessageQueue =>
            _MessageQueue ?? (
            _MessageQueue = new TcpMessageQueue());
        private ICommanderMessageQueue _MessageQueue;

        private CommandCollection Commands =>
            _Commands ?? (
            _Commands = new CommandCollection(Composition.Command.Collection));
        private CommandCollection _Commands;

        private EntryProviderFactoryCollection EntryProviderFactory =>
            _EntryProviderFactory ?? (
            _EntryProviderFactory = new EntryProviderFactoryCollection(Composition.EntryProviderFactory.Collection));
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

        private void Commander_Logger(object sender, EventArgs e) {
            Logger?.Invoke(this, new CommanderLoggerEventArgs());
        }

        private async Task<Commander> Commander(bool first, CancellationToken cancellationToken) {
            var commander = new Commander {
                Clipboard = Import.Clipboard,
                Commands = Commands,
                Dialog = new DialogState(Import.DialogFactory.Create()),
                EntryProviderFactory = EntryProviderFactory,
                First = first
            };
            await Task.CompletedTask;
            commander.Closed += Commander_Closed;
            commander.Exited += Commander_Exited;
            commander.Loaded += Commander_Loaded;
            commander.Logger += Commander_Logger;
            commander.Themed += Commander_Themed;
            Commanders.Add(commander);
            return commander;
        }

        private void Load(Commander commander) {
            if (null == commander) throw new ArgumentNullException(nameof(commander));
            OnLoaded(new CommanderLoadedEventArgs(commander, commander.First));
        }

        private async Task Service() {
            Load(await Commander(first: true, CancellationToken.None));
            await foreach (var s in MessageQueue.Read(default)) {
                if (Log.Info()) {
                    Log.Info($"{nameof(MessageQueue.Read)} > {s}");
                }
                //var command = CommandLine.Parser.Parse<CommanderCommand>(s);
                Load(await Commander(first: false, CancellationToken.None));
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
        public event CommanderLoggerEventHandler Logger;

        public IEnumerable<IComponentResource> ComponentResources =>
            Composition.ComponentResource.Collection;

        public async Task Post(string message, CancellationToken cancellationToken) {
            await MessageQueue.Write(message, cancellationToken);
        }

        public void Begin() {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            TaskHandler.Begin(Service);
        }

        public static class Import {
            public static IClipboard Clipboard { get; set; }
            public static IDialogManagerFactory DialogFactory { get; set; }
        }
    }
}
