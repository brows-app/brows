using Brows.Gui;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class Panel : Notifier, IPanel, IControlled<IPanelController> {
        private static readonly ILog Log = Logging.For(typeof(Panel));

        private readonly PanelInput Input = new();
        private readonly PanelHistory History = new();

        private void LoadCurrentHistory() {
            History.CurrentLoadOpportunity(Provider, loaded: () => {
                if (Active) {
                    Activate();
                }
            });
        }

        private void UpdatePanelState() {
            State = PanelState.For(this);
        }

        private void Drop(IPanelDrop data) {
            var provider = Provider;
            if (provider != null) {
                var op = Commander.Operator;
                if (op != null) {
                    op.Operate("Drop", async (progress, token) => {
                        await provider.Drop(data, progress, token);
                    });
                }
            }
        }

        private void EntryObservation_DropChanged(object sender, EventArgs e) {
            var drop = Provider?.Observation?.Drop;
            if (drop != null) {
                Drop(drop);
            }
        }

        private void EntryObservation_SortingChanged(object sender, EventArgs e) {
            UpdatePanelState();
        }

        private void EntryObservation_SelectedChanged(object sender, EventArgs e) {
            UpdatePanelState();
        }

        private void EntryObservation_ControllerChanged(object sender, EventArgs e) {
            LoadCurrentHistory();
        }

        private void EntryObservation_ObservedChanged(object sender, EventArgs e) {
            LoadCurrentHistory();
            UpdatePanelState();
        }

        private void EntryObservation_CurrentChanged(object sender, EventArgs e) {
            var entries = Provider?.Observation;
            if (entries != null && entries == sender) {
                var entry = entries.Current();
                if (entries.ManualInteraction) {
                    var history = History.Current;
                    if (history != null) {
                        history.CurrentEntryID = entry == null
                            ? history.CurrentEntryID
                            : entry.ID;
                    }
                }
            }
        }

        private void Controller_Drop(object sender, EventArgs e) {
            var drop = Controller?.Dropped;
            if (drop != null) {
                Drop(drop);
            }
        }

        private void Controller_FocusedChanged(object sender, EventArgs e) {
            var control = Controller;
            if (control != null) {
                var focused = control.Focused;
                Active = Active || focused;
                Activated = focused;
            }
        }

        private void Controller_SizeChanged(object sender, EventArgs e) {
            var control = Controller;
            if (control != null) {
                ControlWidth = control.Width;
                ControlHeight = control.Height;
            }
        }

        private void Controller_Activated(object sender, EventArgs e) {
            Activate();
        }

        private void Stop() {
            if (Log.Info()) {
                Log.Info(nameof(Stop));
            }
            var provider = Provider;
            if (provider != null) {
                using (provider) {
                    provider.Observation.DropChanged -= EntryObservation_DropChanged;
                    provider.Observation.CurrentChanged -= EntryObservation_CurrentChanged;
                    provider.Observation.SortingChanged -= EntryObservation_SortingChanged;
                    provider.Observation.ObservedChanged -= EntryObservation_ObservedChanged;
                    provider.Observation.SelectedChanged -= EntryObservation_SelectedChanged;
                    provider.Observation.ControllerChanged -= EntryObservation_ControllerChanged;
                    provider.End();
                }
            }
            State = null;
            Activated = false;
            Input.Clear();
        }

        private void Start(IProvider provider) {
            if (null == provider) throw new ArgumentNullException(nameof(provider));
            if (Log.Info()) {
                Log.Info(nameof(Start) + " > " + provider?.GetType()?.Name);
            }
            provider.Observation.DropChanged += EntryObservation_DropChanged;
            provider.Observation.CurrentChanged += EntryObservation_CurrentChanged;
            provider.Observation.SortingChanged += EntryObservation_SortingChanged;
            provider.Observation.ObservedChanged += EntryObservation_ObservedChanged;
            provider.Observation.SelectedChanged += EntryObservation_SelectedChanged;
            provider.Observation.ControllerChanged += EntryObservation_ControllerChanged;
            Provider = provider;
            Provider.Begin();
        }

        private async Task<bool> TryStart(string id, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(TryStart) + " > " + id);
            }
            var provider = await Providers.CreateFor(id, this, cancellationToken);
            if (provider == null) {
                return false;
            }
            SharedHistory.Add(provider.ID);
            History.Set(provider.ID);
            History.SettingBack = false;
            History.SettingForward = false;
            Stop();
            Start(provider);
            return true;
        }

        public Action<Panel> Activating {
            get => _Activating;
            set => Change(ref _Activating, value, nameof(Activating));
        }
        private Action<Panel> _Activating;

        public double ControlWidth {
            get => _ControlWidth;
            private set => Change(ref _ControlWidth, value, nameof(ControlWidth));
        }
        private double _ControlWidth;

        public double ControlHeight {
            get => _ControlHeight;
            private set => Change(ref _ControlHeight, value, nameof(ControlHeight));
        }
        private double _ControlHeight;

        public IProvider Provider {
            get => _Provider;
            private set => Change(ref _Provider, value, nameof(Provider));
        }
        private IProvider _Provider;

        public int Column {
            get => _Column;
            set => Change(ref _Column, value, nameof(Column));
        }
        private int _Column;

        public PanelState State {
            get => _State ?? (_State = new PanelState());
            private set => Change(ref _State, value, nameof(State));
        }
        private PanelState _State;

        public PanelSecret Secret {
            get => _Secret;
            private set => Change(ref _Secret, value, nameof(Secret));
        }
        private PanelSecret _Secret;

        public PanelHistoryShared SharedHistory {
            get => _SharedHistory ?? (_SharedHistory = new());
            set => Change(ref _SharedHistory, value, nameof(SharedHistory));
        }
        private PanelHistoryShared _SharedHistory;

        public bool Active {
            get => _Active;
            private set {
                if (Change(ref _Active, value, nameof(Active))) {
                    if (Active) {
                        Activating?.Invoke(this);
                    }
                }
            }
        }
        private bool _Active;

        public bool Activated {
            get => _Activated;
            private set => Change(ref _Activated, value, nameof(Activated));
        }
        private bool _Activated;

        public Commander Commander { get; }
        public ProviderFactorySet Providers { get; }

        public Panel(ProviderFactorySet providers, Commander commander) {
            Providers = providers ?? throw new ArgumentNullException(nameof(providers));
            Commander = commander ?? throw new ArgumentNullException(nameof(commander));
        }

        public async Task Post(IMessage message, CancellationToken token) {
            var task = Provider?.Take(message, token);
            if (task != null) {
                await task;
            }
        }

        public async Task<bool> Provide(string id, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(nameof(Provide) + " > " + id);
            }
            var started = await TryStart(id, token);
            if (started) {
                return true;
            }
            return false;
        }

        public void Close() {
            if (Log.Info()) {
                Log.Info(nameof(Close));
            }
            Stop();
        }

        public async Task<bool> HistoryBack(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(nameof(HistoryBack));
            }
            var history = History;
            if (history != null) {
                var item = history.Back();
                if (item != null) {
                    history.SettingBack = true;
                    return await Provide(item.ID, token);
                }
            }
            return false;
        }

        public async Task<bool> HistoryForward(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(nameof(HistoryForward));
            }
            var history = History;
            if (history != null) {
                var item = history.Forward();
                if (item != null) {
                    history.SettingForward = true;
                    return await Provide(item.ID, token);
                }
            }
            return false;
        }

        public void Reset() {
            Provider?.Observation?.DataView?.Reset();
        }

        public void Refresh() {
            Provider?.Refresh();
        }

        public IReadOnlyList<IEntry> Entries =>
            Provider?.Observation?.Observed ?? Array.Empty<IEntry>();

        public IReadOnlySet<IEntry> Selection =>
            Provider?.Observation?.Selected ?? new HashSet<IEntry>();

        public void Activate() {
            if (Log.Info()) {
                Log.Info(nameof(Activate));
            }
            Active = true;
            Activated = Provider?.Observation?.Focus() == true;
        }

        public void Deactivate() {
            if (Log.Info()) {
                Log.Info(nameof(Deactivate));
            }
            Active = false;
            Activated = false;
        }

        public bool Text(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                if (string.IsNullOrWhiteSpace(Input.Aggregate)) {
                    return false;
                }
            }
            var observation = Provider?.Observation;
            if (observation == null) {
                return false;
            }
            var observed = observation.Observed;
            if (observed == null) {
                return false;
            }
            var match = Input.Text(value, observed).FirstOrDefault();
            if (match != null) {
                observation.Current(match);
            }
            return true;
        }

        bool IPanel.HasWindow(out object native) {
            native = Commander.NativeWindow();
            return native != null;
        }

        bool IPanel.HasView(out IEntryDataView view) {
            view = Provider?.Observation?.DataView;
            return view != null;
        }

        bool IPanel.HasSelection(out IReadOnlySet<IEntry> entries) {
            entries = Selection;
            return entries.Count > 0;
        }

        bool IPanel.HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) {
            var selection = Selection;
            if (selection.Count == 0) {
                entries = null;
                return false;
            }
            entries = selection.OfType<TEntry>().ToHashSet();
            return entries.Count > 0;
        }

        bool IPanel.HasEntry(out IEntry current) {
            current = Provider?.Observation?.Current();
            return current != null;
        }

        bool IPanel.HasEntries(out IReadOnlyList<IEntry> entries) {
            entries = Entries;
            return entries.Count > 0;
        }

        bool IPanel.HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) {
            var e = Entries;
            if (e.Count == 0) {
                entries = null;
                return false;
            }
            entries = e.OfType<TEntry>().ToList();
            return entries.Count > 0;
        }

        bool IPanel.HasProvider<TProvider>(out TProvider provider) {
            provider = Provider as TProvider;
            return provider != null;
        }

        bool IPanel.HasProviderService<TService>(out IProvider provider, out TService service) {
            provider = Provider;
            service = provider?.Import<TService>();
            return service != null;
        }

        async Task<SecureString> IPanel.GetSecret(string promptFormat, IEnumerable<string> promptArgs, CancellationToken token) {
            var secret = Secret = new PanelSecret {
                PromptArgs = promptArgs,
                PromptFormat = promptFormat
            };
            try {
                secret.Focus();
                return await secret.Submitted(token);
            }
            finally {
                Secret = null;
            }
        }

        IPanelController IControlled<IPanelController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.Activated -= Controller_Activated;
                        oldValue.Drop -= Controller_Drop;
                        oldValue.FocusedChanged -= Controller_FocusedChanged;
                        oldValue.SizeChanged -= Controller_SizeChanged;
                    }
                    if (newValue != null) {
                        newValue.Activated += Controller_Activated;
                        newValue.Drop += Controller_Drop;
                        newValue.FocusedChanged += Controller_FocusedChanged;
                        newValue.SizeChanged += Controller_SizeChanged;
                    }
                    Controller = newValue;
                }
            }
        }
        private IPanelController Controller;
    }
}
