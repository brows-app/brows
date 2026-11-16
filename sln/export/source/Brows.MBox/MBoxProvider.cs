using Brows.Entries;
using Brows.MBox.Format;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Shows the messages of a mailbox file.
/// </summary>
internal sealed class MBoxProvider : Provider<MBoxEntry, MBoxConfig> {
    private static readonly ILog Log = Logging.For(typeof(MBoxProvider));

    private readonly Lock _Locker = new();
    private readonly Lock _SettingsLocker = new();
    private readonly SemaphoreSlim _RefreshLocker = new(1, 1);
    private readonly MBoxConfig _Config;
    private readonly bool _ConfigOverride;
    private MBoxSession _Session;
    private MBoxPreviewCache _Cache;
    private TaskCompletionSource _DecodeChanged = NewDecodeSignal();
    private int _ActiveDecodeCount;
    private MBoxConfig _ObservedSettings;
    private bool _Ended;

    internal enum IndexStage {
        Scan,
        ReadHeaders,
        Metadata,
    }

    /// <summary>
    /// Gets the configuration of the provider.
    /// </summary>
    /// <remarks>
    /// The framework config is loaded during provider initialization. A supplied config is an
    /// explicit feature-level override used by the provider's construction path.
    /// </remarks>
    private MBoxConfig Settings {
        get {
            var config = _ConfigOverride ? _Config : Config ?? _Config;
            lock (_SettingsLocker) {
                var providerIsActive = _Ended == false;
                var settingsChanged = ReferenceEquals(_ObservedSettings, config) == false;
                var shouldObserveSettings = providerIsActive && settingsChanged;
                if (shouldObserveSettings) {
                    if (_ObservedSettings is not null) {
                        _ObservedSettings.MaxActivePreviewDecodesChanged -= Settings_MaxActivePreviewDecodesChanged;
                    }
                    _ObservedSettings = config;
                    _ObservedSettings.MaxActivePreviewDecodesChanged += Settings_MaxActivePreviewDecodesChanged;
                }
            }
            return config;
        }
    }

    /// <summary>
    /// Gets the state of the provider.
    /// </summary>
    public MBoxProviderState State {
        get => field;
        private set => Change(ref field, value);
    } = MBoxProviderState.Loading;

    /// <summary>
    /// Gets the message that describes the state of the provider, or an empty string when there
    /// is none.
    /// </summary>
    public string Status {
        get => field ?? string.Empty;
        private set => Change(ref field, value);
    }

    /// <summary>
    /// Gets the message that describes the error of the provider, or an empty string when there
    /// is none.
    /// </summary>
    public string Error {
        get => field ?? string.Empty;
        private set => Change(ref field, value);
    }

    /// <summary>
    /// Gets whether a refresh is running.
    /// </summary>
    public bool Refreshing {
        get => field;
        private set => Change(ref field, value);
    }

    /// <summary>
    /// Gets the number of messages that were indexed.
    /// </summary>
    public int IndexedCount {
        get => field;
        private set => Change(ref field, value);
    }

    /// <summary>
    /// Gets the identifier of the generation of the data that the provider shows, or an empty
    /// string when it shows none.
    /// </summary>
    public string ActiveGenerationId {
        get => field ?? string.Empty;
        private set => Change(ref field, value);
    }

    /// <summary>
    /// Gets the path of the mailbox.
    /// </summary>
    public string Path {
        get;
    }

    /// <summary>
    /// Gets the parent of the mailbox, which is the directory that holds it.
    /// </summary>
    public sealed override string Parent {
        get {
            var directory = System.IO.Path.GetDirectoryName(Path);
            return string.IsNullOrEmpty(directory) ? Path : directory;
        }
    }

    /// <summary>
    /// Gets the name of the provider, which is the path of the mailbox.
    /// </summary>
    public string Name =>
        Path;

    /// <summary>
    /// Gets the icon of the provider.
    /// </summary>
    public object Icon =>
        null;

    /// <summary>
    /// Gets the factory that created the provider.
    /// </summary>
    public MBoxProviderFactory Factory {
        get;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxProvider"/> class.
    /// </summary>
    /// <param name="factory">
    /// The factory that created the provider.
    /// </param>
    /// <param name="path">
    /// The canonical path of the mailbox.
    /// </param>
    /// <param name="config">
    /// The configuration of the provider. When it is <see langword="null"/>, the configuration
    /// of the provider is loaded from its config file.
    /// </param>
    public MBoxProvider(MBoxProviderFactory factory, string path, MBoxConfig config = null)
    : base(path) {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        _Config = config ?? new MBoxConfig();
        if (config != null) {
            /*
             * A config that the caller supplies is used as it is, and the config file of the
             * feature is not read. When the caller supplies none, the framework reads the
             * config file, and the feature uses what it read.
             */
            _ConfigOverride = true;
        }
    }

    /// <summary>
    /// Reads the mailbox and provides its messages.
    /// </summary>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    protected sealed override async Task Begin(CancellationToken token) {
        await Refresh(token);
    }

    /// <summary>
    /// Reads the mailbox again, and provides its messages.
    /// </summary>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    protected sealed override async Task Refresh(CancellationToken token) {
        try {
            await _RefreshLocker.WaitAsync(token);
        }
        catch (OperationCanceledException) {
            /*
             * The read was cancelled before it began, so the messages that the provider shows
             * are left as they are.
             */
            SetState(
                MBoxProviderState.Cancelled,
                MBoxText.Value("MBoxProvider_Status_Cancelled"),
                string.Empty);
            return;
        }
        try {
            lock (_Locker) {
                if (_Ended) {
                    return;
                }
            }
            State = MBoxProviderState.Loading;
            Status = MBoxText.Value("MBoxProvider_Status_Loading");
            Error = string.Empty;
            Refreshing = true;
            await Prepare(token);
        }
        finally {
            Refreshing = false;
            _RefreshLocker.Release();
        }
    }

    internal Action<IndexStage> IndexStageForTesting { get; set; }

    internal Func<CancellationToken, Task> IndexGateForTesting { get; set; }

    internal Func<CancellationToken, Task> DecodeGateForTesting { get; set; }

    /// <summary>
    /// Gets the messages that the provider provides.
    /// </summary>
    /// <returns>
    /// The messages that the provider provides.
    /// </returns>
    internal IReadOnlyList<MBoxEntry> Entries =>
        Provided;

    /// <summary>
    /// Retires the provider, and releases the mailbox that it holds.
    /// </summary>
    protected sealed override void End() {
        MBoxSession session;
        MBoxPreviewCache cache;
        lock (_Locker) {
            if (_Ended) {
                return;
            }
            _Ended = true;
            session = _Session;
            cache = _Cache;
            _Session = null;
            _Cache = null;
            lock (_SettingsLocker) {
                if (_ObservedSettings is not null) {
                    _ObservedSettings.MaxActivePreviewDecodesChanged -= Settings_MaxActivePreviewDecodesChanged;
                    _ObservedSettings = null;
                }
            }
        }
        Exception error = null;
        try {
            cache?.Clear();
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            session?.Retire();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        try {
            session?.Dispose();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        try {
            SignalDecodeWaiters();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            Exception error = null;
            try {
                ((IProvider)this).End();
            }
            catch (Exception ex) {
                error = ex;
            }
            try {
                base.Dispose(disposing);
            }
            catch (Exception ex) {
                error ??= ex;
            }
            if (error is not null) {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
            return;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Returns the preview of a message of the active generation of the mailbox.
    /// </summary>
    /// <param name="entry">
    /// The message to preview.
    /// </param>
    /// <param name="token">
    /// The token that cancels the wait of the caller. It does not cancel the work of another
    /// caller.
    /// </param>
    /// <returns>
    /// The preview of the message.
    /// </returns>
    public async Task<MBoxPreviewResult> Preview(MBoxEntry entry, CancellationToken token) {
        if (entry is null) {
            throw new ArgumentNullException(nameof(entry));
        }
        MBoxSession session;
        MBoxPreviewCache cache;
        lock (_Locker) {
            session = _Session;
            cache = _Cache;
        }
        var sessionOrCacheIsMissing = session == null || cache == null;
        if (sessionOrCacheIsMissing) {
            return MBoxPreviewResult.Empty;
        }
        if (string.Equals(entry.GenerationId, session.GenerationId, StringComparison.Ordinal) == false) {
            /*
             * The message belongs to a retired generation, so it is not previewed.
             */
            return MBoxPreviewResult.Empty;
        }
        var key = PreviewKey(session, entry);
        var cached = cache.Get(key);
        if (cached != null) {
            return cached;
        }
        using var lease = session.TryAcquireLease();
        if (lease is null) {
            return MBoxPreviewResult.Empty;
        }
        return await Decode(session, cache, key, entry, token);
    }

    /// <summary>
    /// Returns the key that identifies the preview of a message.
    /// </summary>
    /// <param name="session">
    /// The session that holds the mailbox of the message.
    /// </param>
    /// <param name="entry">
    /// The message to preview.
    /// </param>
    /// <returns>
    /// The key that identifies the preview of the message.
    /// </returns>
    internal static string PreviewKey(MBoxSession session, MBoxEntry entry) {
        return $"{session.GenerationId}|{session.Options.Format}|{entry.Record.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private async Task<MBoxPreviewResult> Decode(MBoxSession session,
                                                 MBoxPreviewCache cache,
                                                 string key,
                                                 MBoxEntry entry,
                                                 CancellationToken token) {
        if (await EnterDecode(token) == false) {
            return MBoxPreviewResult.Empty;
        }
        try {
            var gate = DecodeGateForTesting;
            if (gate is not null) {
                await gate(token);
            }
            var message = await Task.Run(cancellationToken: token, function: () => {
                return session.File.ReadMessage(entry.Record, token);
            });
            var result = new MBoxPreviewResult {
                Text = message.Body.Text,
                Html = message.Body.Html,
                State = message.Body.State,
                Diagnostics = message.Body.Diagnostics,
            };
            lock (_Locker) {
                var resultSessionIsCurrent = _Ended == false && ReferenceEquals(_Session, session);
                if (resultSessionIsCurrent == false) {
                    return MBoxPreviewResult.Empty;
                }
                cache.Add(key, result);
            }
            return result;
        }
        finally {
            ExitDecode();
        }
    }

    private async Task<bool> EnterDecode(CancellationToken token) {
        while (true) {
            Task wait;
            lock (_Locker) {
                if (_Ended) {
                    return false;
                }
                if (_ActiveDecodeCount < Settings.MaxActivePreviewDecodes) {
                    _ActiveDecodeCount++;
                    return true;
                }
                wait = _DecodeChanged.Task;
            }
            await wait.WaitAsync(token);
        }
    }

    private void ExitDecode() {
        TaskCompletionSource signal;
        lock (_Locker) {
            if (_ActiveDecodeCount > 0) {
                _ActiveDecodeCount--;
            }
            signal = _DecodeChanged;
            _DecodeChanged = NewDecodeSignal();
        }
        signal.TrySetResult();
    }

    private void Settings_MaxActivePreviewDecodesChanged(object sender, EventArgs e) {
        SignalDecodeWaiters();
    }

    private async Task Prepare(CancellationToken token) {
        var config = Settings;
        MBoxSession session = null;
        var published = false;
        try {
            var options = MBoxParserOptions.Default.WithFormat(config.Format);
            session = await Task.Run(cancellationToken: token, function: () => {
                var file = new MBoxFile(Path, options);
                return new MBoxSession(file, options);
            });
            var gate = IndexGateForTesting;
            IndexGateForTesting = null;
            if (gate is not null) {
                await gate(token);
            }
            var entries = new List<MBoxEntry>();
            var diagnostics = new List<MBoxDiagnostic>();
            var limitReached = await Task.Run(() => Index(session, config, entries, diagnostics, token), token);
            var nextState = MBoxProviderState.Ready;
            var nextStatus = string.Empty;
            var nextError = string.Empty;
            if (limitReached) {
                nextState = MBoxProviderState.Partial;
                nextStatus = MBoxText.Value("MBoxProvider_Status_Partial");
            }
            else if (entries.Count == 0) {
                if (session.File.Length == 0) {
                    nextState = MBoxProviderState.Empty;
                    nextStatus = MBoxText.Value("MBoxProvider_Status_Empty");
                }
                else {
                /*
                 * A mailbox that holds bytes but no accepted message is not an empty mailbox,
                 * but a malformed one.
                 */
                    nextState = MBoxProviderState.Failed;
                    nextStatus = MBoxText.Value("MBoxProvider_Status_Failed");
                    nextError = MBoxText.Value("MBoxProvider_Error_Invalid");
                }
            }

            if (nextState == MBoxProviderState.Failed) {
                SetState(nextState, nextStatus, nextError);
                return;
            }
            if (await Commit(session, entries) == false) {
                return;
            }
            published = true;
            IndexedCount = entries.Count;
            SetState(nextState, nextStatus, nextError);
        }
        catch (OperationCanceledException) {
            SetState(
                MBoxProviderState.Cancelled,
                MBoxText.Value("MBoxProvider_Status_Cancelled"),
                string.Empty);
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(nameof(Prepare), Path, ex);
            }
            SetState(
                MBoxProviderState.Failed,
                MBoxText.Value("MBoxProvider_Status_Failed"),
                ex.Message);
        }
        finally {
            if (published == false) {
                session?.Retire();
                session?.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads the messages of the mailbox, and adds them to the index of the session.
    /// </summary>
    /// <param name="session">
    /// The session that holds the mailbox.
    /// </param>
    /// <param name="config">
    /// The configuration that bounds the index.
    /// </param>
    /// <param name="entries">
    /// The list that receives the messages.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems that were found.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a limit stopped the index before the end of the mailbox.
    /// </returns>
    private bool Index(MBoxSession session,
                       MBoxConfig config,
                       List<MBoxEntry> entries,
                       List<MBoxDiagnostic> diagnostics,
                       CancellationToken token) {
        var maxEntries = Math.Max(0, config.MaxEntries);
        var maxTextBytes = Math.Max(0, config.MaxIndexTextBytes);
        var indexBytes = 0L;
        IndexStageForTesting?.Invoke(IndexStage.Scan);
        foreach (var record in session.File.Scan(diagnostics.Add, token)) {
            if (entries.Count >= maxEntries) {
                return true;
            }
            MBoxMessage message;
            IndexStageForTesting?.Invoke(IndexStage.ReadHeaders);
            message = session.File.ReadHeaders(record, token);
            IndexStageForTesting?.Invoke(IndexStage.Metadata);
            var metadata = MBoxMetadataFactory.Create(message, record, token);
            var size = MBoxMetadataFactory.Size(metadata);
            if (indexBytes + size > maxTextBytes) {
                return true;
            }
            indexBytes += size;
            session.Add(record);
            entries.Add(new MBoxEntry(this, session, record, metadata));
        }
        return false;
    }

    private async Task<bool> Commit(MBoxSession session, List<MBoxEntry> entries) {
        var previousEntries = Provided.ToArray();
        MBoxSession previous;
        MBoxPreviewCache previousCache;
        var previousGeneration = ActiveGenerationId;
        lock (_Locker) {
            if (_Ended) {
                return false;
            }
            previous = _Session;
            previousCache = _Cache;
        }
        try {
            if (previousEntries.Length > 0) {
                await Revoke(previousEntries, CancellationToken.None);
            }
            lock (_Locker) {
                if (_Ended) {
                    return false;
                }
                _Session = session;
                _Cache = new MBoxPreviewCache(Settings.MaxPreviewCacheBytes, Settings.MaxPreviewCacheCount);
            }
            ActiveGenerationId = session.GenerationId;
            if (entries.Count > 0) {
                await Provide(entries, CancellationToken.None);
            }
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(nameof(Commit), Path, ex);
            }
            var ended = false;
            lock (_Locker) {
                ended = _Ended;
                if (ended == false) {
                    _Session = previous;
                    _Cache = previousCache;
                }
            }
            if (ended) {
                previousCache?.Clear();
                previous?.Retire();
                previous?.Dispose();
            }
            else {
                try {
                    ActiveGenerationId = previousGeneration;
                }
                catch (Exception restoreException) {
                    if (Log.Error()) {
                        Log.Error(nameof(Commit), Path, restoreException);
                    }
                }
                await RestoreEntries(previousEntries);
            }
            throw;
        }
        bool isEnded;
        lock (_Locker) {
            isEnded = _Ended;
        }
        if (isEnded) {
            previousCache?.Clear();
            previous?.Retire();
            previous?.Dispose();
            return false;
        }
        previousCache?.Clear();
        previous?.Retire();
        previous?.Dispose();
        return true;
    }

    private async Task RestoreEntries(IReadOnlyList<MBoxEntry> entries) {
        var current = Provided.ToArray();
        if (current.Length > 0) {
            try {
                await Revoke(current, CancellationToken.None);
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(nameof(RestoreEntries), Path, ex);
                }
            }
        }
        if (entries.Count > 0) {
            try {
                await Provide(entries, CancellationToken.None);
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(nameof(RestoreEntries), Path, ex);
                }
            }
        }
    }

    private void SetState(MBoxProviderState state, string status, string error) {
        lock (_Locker) {
            if (_Ended) {
                return;
            }
        }
        State = state;
        Status = status;
        Error = error ?? string.Empty;
    }

    private void SignalDecodeWaiters() {
        TaskCompletionSource signal;
        lock (_Locker) {
            signal = _DecodeChanged;
            _DecodeChanged = NewDecodeSignal();
        }
        signal.TrySetResult();
    }

    private static TaskCompletionSource NewDecodeSignal() {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
