using Brows.Conf;
using Brows.Entries;
using Brows.Gui;
using Brows.Panels.PanelSecrets;
using Brows.Providers;
using Brows.Requests;
using Brows.Security;
using Brows.Sys;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Panels;

internal sealed class Panel : Notifier, IPanel, IControlled<IPanelController> {
    private static readonly ILog Log = Logging.For(typeof(Panel));

    /*
     * A saved secret is stored in the secrets data file under the key
     * requested by the provider combined with the type of the secret. The
     * stored value is protected with the entropy password, which the user
     * enters whenever it is needed and which is kept in memory only for
     * the lifetime of the process; nothing about it is written to disk.
     */

    private readonly SemaphoreSlim LifecycleGate = new(1, 1);
    private readonly Lock CloseLocker = new();
    private readonly CancellationTokenSource Lifetime = new();
    private readonly CancellationToken LifetimeToken;
    private Task CloseTask;
    private volatile bool Closing;
    /*
     * A candidate can fail disposal before becoming the current provider. Retain
     * its ownership so another navigation or panel shutdown can retry cleanup.
     */
    private IProvider ProspectiveProvider;

    private readonly PanelInput Input = new();
    private readonly PanelHistory History = new();

    private IConfiguration<SecretStore> SecretStore =>
        field ??= ConfigData.Data<SecretStore>("secrets");

    private IConfigurationData ConfigData =>
        Commander?.Import?.Find<IConfigurationData>();

    private IEntryObservation Observation =>
        (Provider as IEntryProvider)?.Observation;

    private static string SecretStoreKey(string key, Type type) {
        return $"{key}|{type.Name}";
    }

    private static SecureString PasswordOf(PanelSecret secret) {
        return secret switch {
            SecretConfirmPassword confirmPassword => confirmPassword.Password,
            SecretPassword password => password.Password,
            _ => null
        };
    }

    private async Task<PanelSecret> Prompt(PanelSecret secret, CancellationToken token) {
        Secret = secret;
        try {
            secret.Focus();
            return await secret.Submitted(token);
        }
        finally {
            Secret = null;
        }
    }

    private async Task<bool> EntropyReady(bool retry, CancellationToken token) {
        var request = Commander?.Import?.Find<IRequestFactory>();
        var getEntropy = Commander?.Import?.Find<IGetEntropy>();
        var setEntropy = Commander?.Import?.Find<ISetEntropy>();
        if (request is null || getEntropy is null || setEntropy is null) {
            return false;
        }
        var entropy = default(SecureString);
        var worked = await getEntropy.Work(set: value => entropy = value, token);
        if (worked && entropy is not null) {
            return true;
        }
        var store = await SecretStore.Load(token);
        /*
         * The entropy password is verified by unprotecting a saved secret
         * with it, because all saved secrets are protected with the same
         * entropy. Without a saved secret there is nothing to verify, so
         * the user chooses the password instead of entering it.
         */
        var verify = store.Secret.Values.FirstOrDefault();
        for (; ; ) {
            token.ThrowIfCancellationRequested();
            PanelSecret choice;
            if (verify is null) {
                choice = new SecretConfirmPassword(request) {
                    PromptFormat = retry
                        ? "PanelSecret_Prompt_EntropyChooseMismatch"
                        : "PanelSecret_Prompt_EntropyChoose"
                };
            }
            else {
                choice = new SecretPassword(request) {
                    PromptFormat = retry
                        ? "PanelSecret_Prompt_EntropyEnterFailed"
                        : "PanelSecret_Prompt_EntropyEnter"
                };
            }
            var submitted = await Prompt(choice, token);
            if (submitted is null) {
                return false;
            }
            var password = PasswordOf(submitted);
            if (password is null) {
                /*
                 * The two entries of the confirmation did not match.
                 */
                retry = true;
                continue;
            }
            if (verify is not null) {
                try {
                    await SecurityData.Unprotect(Convert.FromBase64String(verify), password, token);
                }
                catch (CryptographicException) {
                    retry = true;
                    continue;
                }
            }
            setEntropy.Work(password);
            return true;
        }
    }

    private async Task<string> SecretSaved(string storeKey, CancellationToken token) {
        if (storeKey is null) {
            return null;
        }
        var store = await SecretStore.Load(token);
        return store.Secret.TryGetValue(storeKey, out var saved) ? saved : null;
    }

    private async Task<bool> SecretRestore(PanelSecret secret, string saved, CancellationToken token) {
        for (var retry = false; ; retry = true) {
            if (await EntropyReady(retry, token) == false) {
                return false;
            }
            try {
                var data = await SecurityData.Unprotect(Convert.FromBase64String(saved), token);
                secret.Decode(data);
                return true;
            }
            catch (CryptographicException) {
                /*
                 * The saved secret was protected with a different entropy
                 * password, so the user gets to enter it again.
                 */
                Commander?.Import?.Find<ISetEntropy>()?.Work(null);
            }
        }
    }

    private async Task<bool> SecretSave(string storeKey, PanelSecret secret, CancellationToken token) {
        if (storeKey is null) {
            return false;
        }
        if (await EntropyReady(retry: false, token) == false) {
            return false;
        }
        try {
            var data = secret.Encode();
            var protectedData = await SecurityData.Protect(data, token);
            var store = await SecretStore.Load(token);
            var saved = new Dictionary<string, string>(store.Secret) {
                [storeKey] = Convert.ToBase64String(protectedData)
            };
            store.Secret = saved;
            return true;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(ex);
            }
            return false;
        }
    }

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
        var drop = Observation?.Drop;
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
        var entries = Observation;
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

    private void Provider_ControllerChanged(object sender, EventArgs e) {
        if (Active) {
            Activate();
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

    private static async Task DisposeProviderAsync(IProvider provider) {
        try {
            provider.End();
        }
        finally {
            if (provider is IAsyncDisposable asynchronous) {
                await asynchronous.DisposeAsync();
            }
            else {
                provider.Dispose();
            }
        }
    }

    private async Task RetireProspectiveAsync() {
        var provider = ProspectiveProvider;
        if (provider is not null) {
            await DisposeProviderAsync(provider);
            ProspectiveProvider = null;
        }
    }

    private async Task StopAsync() {
        var provider = Provider;
        if (provider is Provider model) {
            await model.Detail.ShutdownAsync();
        }
        if (provider is not null) {
            provider.ControllerChanged -= Provider_ControllerChanged;
            if (provider is IEntryProvider entryProvider) {
                var observation = entryProvider.Observation;
                observation.DropChanged -= EntryObservation_DropChanged;
                observation.CurrentChanged -= EntryObservation_CurrentChanged;
                observation.SortingChanged -= EntryObservation_SortingChanged;
                observation.ObservedChanged -= EntryObservation_ObservedChanged;
                observation.SelectedChanged -= EntryObservation_SelectedChanged;
                observation.ControllerChanged -= EntryObservation_ControllerChanged;
            }
            await DisposeProviderAsync(provider);
        }
        Provider = null;
        State = null;
        Activated = false;
        Input.Clear();
    }

    private async Task CloseCoreAsync() {
        /*
         * Yield before cancellation callbacks so CloseTask is published before a
         * callback can request the same close again.
         */
        await Task.Yield();
        Lifetime.Cancel();
        await LifecycleGate.WaitAsync();
        try {
            await StopAsync();
            await RetireProspectiveAsync();
            Lifetime.Dispose();
        }
        finally {
            LifecycleGate.Release();
        }
    }
    private void Start(IProvider provider) {
        if (null == provider) throw new ArgumentNullException(nameof(provider));
        if (Log.Info()) {
            Log.Info(nameof(Start) + " > " + provider?.GetType()?.Name);
        }
        if (provider is IEntryProvider entryProvider) {
            var observation = entryProvider.Observation;
            observation.DropChanged += EntryObservation_DropChanged;
            observation.CurrentChanged += EntryObservation_CurrentChanged;
            observation.SortingChanged += EntryObservation_SortingChanged;
            observation.ObservedChanged += EntryObservation_ObservedChanged;
            observation.SelectedChanged += EntryObservation_SelectedChanged;
            observation.ControllerChanged += EntryObservation_ControllerChanged;
        }
        provider.ControllerChanged += Provider_ControllerChanged;
        Provider = provider;
        ProspectiveProvider = null;
        UpdatePanelState();
        Provider.Begin();
    }

    private async Task<bool> TryStart(string id, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(TryStart) + " > " + id);
        }
        await RetireProspectiveAsync();
        var provider = await Providers.CreateFor(id, this, token);
        if (provider == null) {
            return false;
        }
        ProspectiveProvider = provider;
        try {
            token.ThrowIfCancellationRequested();
            await StopAsync();
            token.ThrowIfCancellationRequested();
        }
        catch {
            await RetireProspectiveAsync();
            throw;
        }
        SharedHistory.Add(provider.ID);
        History.Set(provider.ID);
        History.SettingBack = false;
        History.SettingForward = false;
        Start(provider);
        return true;
    }

    public Action<Panel> Activating {
        get;
        set => Change(ref field, value, nameof(Activating));
    }

    public double ControlWidth {
        get;
        private set => Change(ref field, value, nameof(ControlWidth));
    }

    public double ControlHeight {
        get;
        private set => Change(ref field, value, nameof(ControlHeight));
    }

    public IProvider Provider {
        get;
        private set => Change(ref field, value, nameof(Provider));
    }

    public int Column {
        get;
        set => Change(ref field, value, nameof(Column));
    }

    public PanelState State {
        get => field ??= new PanelState();
        private set => Change(ref field, value, nameof(State));
    }

    public PanelSecret Secret {
        get;
        private set => Change(ref field, value, nameof(Secret));
    }

    public PanelHistoryShared SharedHistory {
        get => field ??= new();
        set => Change(ref field, value, nameof(SharedHistory));
    }

    public bool Active {
        get;
        private set {
            if (Change(ref field, value, nameof(Active))) {
                if (field) {
                    Activating?.Invoke(this);
                }
            }
        }
    }

    public bool Activated {
        get;
        private set => Change(ref field, value, nameof(Activated));
    }

    public Commander Commander { get; }
    public ProviderFactorySet Providers { get; }

    public Panel(ProviderFactorySet providers, Commander commander) {
        LifetimeToken = Lifetime.Token;
        Providers = providers ?? throw new ArgumentNullException(nameof(providers));
        Commander = commander ?? throw new ArgumentNullException(nameof(commander));
    }

    public async Task Post(ISystemMessage message, CancellationToken token) {
        var task = Provider?.Take(message, token);
        if (task != null) {
            await task;
        }
    }

    public async Task<bool> Provide(string id, CancellationToken token) {
        if (Closing) {
            return false;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, LifetimeToken);
        await LifecycleGate.WaitAsync(linked.Token);
        try {
            if (Closing) {
                return false;
            }
            return await TryStart(id, linked.Token);
        }
        finally {
            LifecycleGate.Release();
        }
    }

    public Task CloseAsync() {
        lock (CloseLocker) {
            var canRetry = CloseTask is null || CloseTask.IsFaulted || CloseTask.IsCanceled;
            if (canRetry) {
                Closing = true;
                CloseTask = CloseCoreAsync();
            }
            return CloseTask;
        }
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
        Observation?.DataView?.Reset();
    }

    public async Task<bool> Refresh(CancellationToken token) {
        var provider = Provider;
        if (provider != null) {
            var task = provider.Refresh(token);
            if (task != null) {
                await task;
            }
            return true;
        }
        return false;
    }

    public IReadOnlyList<IEntry> Entries =>
        Observation?.Observed ?? Array.Empty<IEntry>();

    public IReadOnlySet<IEntry> Selection =>
        Observation?.Selected ?? new HashSet<IEntry>();

    public void Activate() {
        if (Log.Info()) {
            Log.Info(nameof(Activate));
        }
        Active = true;
        Activated = Provider?.Focus() == true;
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
        var observation = Observation;
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
        view = Observation?.DataView;
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
        current = Observation?.Current();
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

    bool IPanel.HasProvider(out IProvider provider) {
        provider = Provider;
        return provider != null;
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

    async Task<TSecret> IPanel.GetSecret<TSecret>(string key,
                                                  string promptFormat,
                                                  IEnumerable<string> promptArgs,
                                                  CancellationToken token) {
        var request = Commander?.Import?.Find<IRequestFactory>();
        var storeKey = key is null ? null : SecretStoreKey(key, typeof(TSecret));
        var saved = await SecretSaved(storeKey, token);
        if (saved is not null) {
            var restored = PanelSecret.Create<TSecret>(request);
            if (await SecretRestore(restored, saved, token)) {
                return (TSecret)(object)restored;
            }
        }
        var secret = PanelSecret.Create<TSecret>(request);
        secret.PromptArgs = promptArgs;
        secret.PromptFormat = promptFormat;
        var submitted = await Prompt(secret, token);
        if (submitted is TSecret result) {
            await SecretSave(storeKey, submitted, token);
            return result;
        }
        return default;
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
