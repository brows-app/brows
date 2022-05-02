using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Gui;
    using Logger;
    using Threading.Tasks;

    public class Panel : NotifyPropertyChanged, IPanel, IEntryProviderTarget, IPayloadTarget, IControlled<IPanelController> {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(Panel)));
        private ILog _Log;

        private PanelHistory History { get; } = new PanelHistory();
        private TaskHandler TaskHandler { get; } = new TaskHandler<Panel>();
        private EntryCollection EntryCollection { get; } = new EntryCollection();

        private IEntryProvider Provider;
        private IReadOnlySet<string> DataKeyOptions;

        private IEntry CurrentEntry {
            get => _CurrentEntry;
            set => Change(ref _CurrentEntry, value, nameof(CurrentEntry));
        }
        private IEntry _CurrentEntry;

        private void EntryCollection_CurrentChanged(object sender, EventArgs e) {
            var entry = CurrentEntry = EntryCollection.CurrentItem;
            var history = History.Current;
            if (history != null) {
                history.CurrentEntryID = entry?.ID;
            }
            var preview = Preview;
            if (preview != null) {
                preview.Set(entry);
            }
        }

        private void Control_Drop(object sender, DropEventArgs e) {
            if (e != null) {
                if (TaskHandler.Idle) {
                    TaskHandler.Begin(Deploy(e.Payload, default));
                }
            }
        }

        private void Control_FocusedChanged(object sender, EventArgs e) {
            var control = Controller;
            if (control != null) {
                var focused = control.Focused;
                Active = Active || focused;
                Activated = focused;
            }
        }

        private void Control_SizeChanged(object sender, EventArgs e) {
            var control = Controller;
            if (control != null) {
                ControlWidth = control.Width;
                ControlHeight = control.Height;
            }
        }

        private async Task DeployAsync(IPayload payload, CancellationToken cancellationToken) {
            var d = new PanelDeployment(payload, this, Dialog, Operations, true);
            var op = Provider?.Operator(d);
            if (op != null) {
                await op.Deploy(cancellationToken);
            }
        }

        private void Stop() {
            if (Log.Info()) {
                Log.Info(nameof(Stop));
            }
            var provider = Provider;
            if (provider != null) {
                provider.End();
            }
            var preview = Preview;
            if (preview != null) {
                preview.Set(null);
            }
            Activated = false;
            CurrentEntry = null;
            Input.Clear();

            EntryCollection.CurrentChanged -= EntryCollection_CurrentChanged;
            EntryCollection.Clear();
            EntryCollection.ClearSort();
            EntryCollection.ColumnLookup = null;
            EntryCollection.ColumnDefaults = null;
            EntryCollection.ColumnResolver = null;
        }

        private void Restart(IEntryProvider provider) {
            if (null == provider) throw new ArgumentNullException(nameof(provider));
            if (Log.Info()) {
                Log.Info(
                    nameof(Restart),
                    $"{nameof(provider)} > {provider?.GetType()?.Name}");
            }
            Stop();
            Provider = provider;

            EntryCollection.CurrentChanged += EntryCollection_CurrentChanged;
            EntryCollection.ColumnLookup = provider.DataKeyLookup;
            EntryCollection.ColumnDefaults = provider.DataKeyDefaults;
            EntryCollection.ColumnResolver = provider.DataKeyResolver;
            EntryCollection.ColumnConverters = provider.DataKeyConverters;

            if (DataKeyOptions != provider.DataKeyOptions) {
                DataKeyOptions = provider.DataKeyOptions;
                EntryCollection.ClearColumns();
                EntryCollection.ClearSort();
                EntryCollection.RefreshColumns();
            }
            provider.Begin(this);
            ID = provider.PanelID;
            Icon = provider.Icon;
        }

        private async Task<bool> StartID(string id, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(
                    nameof(StartID),
                    $"{nameof(id)} > {id}");
            }
            var provider = await ProviderFactory.CreateFor(id, cancellationToken);
            if (provider == null) {
                return false;
            }
            SharedHistory.Add(provider.PanelID);
            History.Set(provider.PanelID);
            History.SettingBack = false;
            History.SettingForward = false;
            Restart(provider);
            return true;
        }

        private async Task<bool> StartDefault(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(StartDefault));
            }
            var provider = await ProviderFactory.CreateDefault(cancellationToken);
            if (provider == null) {
                return false;
            }
            Restart(provider);
            return true;
        }

        internal Panel(EntryProviderFactoryCollection providerFactory) {
            ProviderFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
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

        public IEntryCollection Entries =>
            EntryCollection;

        public Preview Preview {
            get => _Preview;
            private set => Change(ref _Preview, value, nameof(Preview));
        }
        private Preview _Preview;

        public Image Icon {
            get => _Icon;
            private set => Change(ref _Icon, value, nameof(Icon));
        }
        private Image _Icon;

        public IPanelController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                    if (oldValue != null) {
                        oldValue.Drop -= Control_Drop;
                        oldValue.FocusedChanged -= Control_FocusedChanged;
                        oldValue.SizeChanged -= Control_SizeChanged;
                    }
                    if (newValue != null) {
                        newValue.Drop += Control_Drop;
                        newValue.FocusedChanged += Control_FocusedChanged;
                        newValue.SizeChanged += Control_SizeChanged;
                    }
                }
            }
        }
        private IPanelController _Controller;

        public int Column {
            get => _Column;
            set => Change(ref _Column, value, nameof(Column));
        }
        private int _Column;

        public PanelInput Input =>
            _Input ?? (
            _Input = new PanelInput { Entries = EntryCollection });
        private PanelInput _Input;

        public IPanelID ID {
            get => _ID;
            private set => Change(ref _ID, value, nameof(ID));
        }
        private IPanelID _ID;

        public PanelIDSet<IPanelID> SharedHistory {
            get => _SharedHistory ?? (_SharedHistory = new PanelIDSet<IPanelID>());
            set => Change(ref _SharedHistory, value, nameof(SharedHistory));
        }
        private PanelIDSet<IPanelID> _SharedHistory;

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

        public PanelPreviewMode PreviewMode {
            get => _PreviewMode;
            set {
                if (Change(ref _PreviewMode, value, nameof(PreviewMode))) {
                    Preview = _PreviewMode == PanelPreviewMode.None
                        ? null
                        : new Preview(CurrentEntry);
                }
            }
        }
        private PanelPreviewMode _PreviewMode;

        public IDialogState Dialog {
            get => _Dialog;
            set => Change(ref _Dialog, value, nameof(Dialog));
        }
        private IDialogState _Dialog;

        public IOperationCollection Operations {
            get => _Operations;
            set => Change(ref _Operations, value, nameof(Operations));
        }
        private IOperationCollection _Operations;

        public EntryProviderFactoryCollection ProviderFactory { get; }

        public object HistoryBackRequest => Request.CreateAsync(
            owner: this,
            execute: async (_, token) => await HistoryBack(token),
            canExecute: _ => true);

        public object HistoryForwardRequest => Request.CreateAsync(
            owner: this,
            execute: async (_, token) => await HistoryForward(token),
            canExecute: _ => true);

        public object OpenParentRequest => Request.CreateAsync(
            owner: this,
            execute: async (_, token) => await OpenParent(token),
            canExecute: _ => true);

        public async Task Open(string id, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Open),
                    $"{nameof(id)} > {id}");
            }
            var started = await StartID(id, cancellationToken);
            if (started == false) {
                started = await StartDefault(cancellationToken);
            }
        }

        public async Task OpenParent(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(OpenParent));
            }
            var provider = Provider;
            if (provider != null) {
                var parent = provider.ParentID;
                if (parent != null) {
                    await Open(parent, cancellationToken);
                }
                else {
                    await Open("", cancellationToken);
                }
            }
        }

        public async Task<bool> OpenCreated(string createdName, CancellationToken cancellationToken) {
            var provider = Provider;
            if (provider != null) {
                var createdID = provider.CreatedID(createdName);
                if (createdID != null) {
                    var opened = await StartID(createdID, cancellationToken);
                    if (opened) {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task Close(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Close));
            }
            Stop();
            await Task.CompletedTask;
        }

        public async Task<bool> HistoryBack(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(HistoryBack));
            }
            var history = History;
            if (history != null) {
                var item = history.Back();
                if (item != null) {
                    history.SettingBack = true;
                    await Open(item.ID, cancellationToken);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> HistoryForward(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(HistoryForward));
            }
            var history = History;
            if (history != null) {
                var item = history.Forward();
                if (item != null) {
                    history.SettingForward = true;
                    await Open(item.ID, cancellationToken);
                    return true;
                }
            }
            return false;
        }

        public Task Refresh(CancellationToken _) {
            if (Log.Info()) {
                Log.Info(nameof(Refresh));
            }

            var provider = Provider;
            if (provider != null) {
                provider.Refresh();
            }
            // TODO: Allow this hard refresh.
            //var id = ID;
            //if (id != null) {
            //    Open(id.Value);
            //}
            return Task.CompletedTask;
        }

        public Task Save(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }

        public IEnumerable<IEntry> Selection() {
            return EntryCollection
                .Where(entry => entry.Selected)
                .ToList();
        }

        public Task Deploy(IPayload payload, CancellationToken cancellationToken, Func<Task> then = null) {
            var task = DeployAsync(payload, cancellationToken);
            if (then != null) {
                var scheduler = TaskScheduler.Default;
                var continuationOptions =
                    TaskContinuationOptions.AttachedToParent |
                    TaskContinuationOptions.ExecuteSynchronously |
                    TaskContinuationOptions.HideScheduler |
                    TaskContinuationOptions.OnlyOnRanToCompletion;
                task = task.ContinueWith(_ => {
                    TaskHandler.Begin(then());
                }, cancellationToken, continuationOptions, scheduler);
            }
            TaskHandler.Begin(task);
            return Task.CompletedTask;
        }

        public Task Deploy(
            CancellationToken cancellationToken,
            IEnumerable<string> copyFiles = null,
            IEnumerable<string> moveFiles = null,
            IEnumerable<string> createFiles = null,
            IEnumerable<string> createDirectories = null,
            IEnumerable<IEntry> copyEntries = null,
            IEnumerable<IEntry> moveEntries = null,
            IEnumerable<IEntry> deleteEntries = null,
            IEnumerable<IEntry> renameEntries = null,
            bool? nativeError = null,
            bool? nativeProgress = null,
            bool? nativeConfirmation = null,
            bool? nativeUndo = null,
            bool? nativeTrash = null,
            bool? nativeRenameOnCollision = null,
            bool? nativeFailEarly = null,
            Func<Task> then = null) {
            return Deploy(cancellationToken: cancellationToken, then: then, payload: new PanelPayload {
                CopyEntries = copyEntries,
                CopyFiles = copyFiles,
                CreateDirectories = createDirectories,
                CreateFiles = createFiles,
                DeleteEntries = deleteEntries,
                MoveEntries = moveEntries,
                MoveFiles = moveFiles,
                NativeConfirmation = nativeConfirmation ?? true,
                NativeError = nativeError ?? true,
                NativeFailEarly = nativeFailEarly ?? false,
                NativeProgress = nativeProgress ?? true,
                NativeRenameOnCollision = nativeRenameOnCollision ?? false,
                NativeTrash = nativeTrash ?? true,
                NativeUndo = nativeUndo ?? false,
                RenameEntries = renameEntries
            });
        }

        public void Activate() {
            if (Log.Info()) {
                Log.Info(nameof(Activate));
            }
            Active = true;
            Activated = EntryCollection.Focus() == true;
        }

        public void Deactivate() {
            if (Log.Info()) {
                Log.Info(nameof(Deactivate));
            }
            Active = false;
            Activated = false;
        }

        IPanelProvider IPanel.Provider => Provider;

        Task IEntryProviderTarget.Add(IEntry entry, CancellationToken cancellationToken) {
            if (entry != null) {
                entry.Begin(this);
                EntryCollection.Add(entry);
                if (EntryCollection.CurrentItem == null) {
                    var currentHistory = History.Current;
                    if (currentHistory != null) {
                        if (currentHistory.CurrentEntryID == entry.ID) {
                            EntryCollection.MoveCurrentItemTo(entry);
                            if (Active) {
                                Activate();
                            }
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        Task IEntryProviderTarget.Remove(IEntry entry, CancellationToken cancellationToken) {
            if (entry != null) {
                entry.End();
                EntryCollection.Remove(entry);
            }
            return Task.CompletedTask;
        }

        async Task<bool> IEntryBrowser.Browse(string id, CancellationToken cancellationToken) {
            var started = await StartID(id, cancellationToken);
            return started;
        }

        bool IPanel.Input(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                if (string.IsNullOrWhiteSpace(Input.Text)) {
                    return false;
                }
            }
            var match = Input.Add(value);
            if (match != null) {
                EntryCollection.MoveCurrentItemTo(match);
            }
            return true;
        }
    }
}
