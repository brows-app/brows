using Brows.Detail;
using Brows.Entries;
using Brows.Gui;
using Brows.MBox.Format;
using Brows.Panels;
using Brows.Providers;
using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Brows.Gui;

/// <summary>
/// Shows the preview of the current message of a mailbox, and keeps it in sync with the message
/// that the observation of the provider selects.
/// </summary>
/// <remarks>
/// The controller shows the message that is current when the observation of the provider raises
/// its change event. Every request has a sequence number, so that a request that finishes late
/// never overwrites the result of a newer request. The requests are read on a background task,
/// and every change of the view is made on the dispatcher of the control.
/// </remarks>
internal sealed class MBoxEntryPreviewController : Controller<IMBoxEntryPreviewController>,
                                                    IMBoxEntryPreviewController {
    private static readonly ILog Log = Logging.For(typeof(MBoxEntryPreviewController));

    private readonly MBoxEntryPreviewControl _Preview;
    private readonly Dispatcher _Dispatcher;
    private EventHandler _ObservationChanged;
    private MBoxEntryPreview _Detail;
    private IEntryObservation _Observation;
    private MBoxProvider _Provider;
    private CancellationTokenSource _RequestSource;
    private int _RequestSequence;
    private long _AttachmentId;

    /// <summary>
    /// Gets whether the preview owns the keyboard focus.
    /// </summary>
    public bool HasKeyboardFocus =>
        _Preview.IsKeyboardFocusWithin;

    /// <summary>
    /// Gets the text of the preview, as it is shown to the user.
    /// </summary>
    internal string Text {
        get => _Preview.TextBox.Text;
        private set => _Preview.TextBox.Text = value;
    }

    /// <summary>
    /// Gets the status of the preview, as it is shown to the user.
    /// </summary>
    internal string Status {
        get => _Preview.StatusText.Text;
        private set => _Preview.StatusText.Text = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxEntryPreviewController"/> class.
    /// </summary>
    /// <param name="element">
    /// The control that shows the preview.
    /// </param>
    public MBoxEntryPreviewController(MBoxEntryPreviewControl element)
    : base(element) {
        _Preview = element ?? throw new ArgumentNullException(nameof(element));
        _Dispatcher = element.Dispatcher;
        element.DataContextChanged += Preview_DataContextChanged;
    }

    /// <summary>
    /// Moves the keyboard focus to the preview.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the preview took the focus.
    /// </returns>
    public bool RequestFocus() {
        var detail = _Detail;
        if (detail is null) {
            return false;
        }
        var attachmentId = _AttachmentId;
        var generation = detail.Provider.Detail.Generation;
        var isCurrentAttachment = IsCurrentAttachment(detail, attachmentId, generation);
        if (isCurrentAttachment == false) {
            return false;
        }
        return _Preview.TextBox.Focus();
    }

    /// <summary>
    /// Moves the keyboard focus to the preview, after the work of the user interface that is
    /// running now ends.
    /// </summary>
    public void RequestFocusLater(IPanel panel, IPanelCollection panels, CancellationToken token) {
        /*
         * The dispatcher does not interrupt the work item that is running now, so the preview
         * takes the focus after the palette closed and the panel gave the focus back to the
         * list.
         */
        var detail = _Detail;
        if (detail is null) {
            return;
        }
        var attachmentId = _AttachmentId;
        var generation = detail.Provider.Detail.Generation;
        _Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
            var attachmentIsCurrent = IsCurrentAttachment(detail, attachmentId, generation);
            var panelIsCurrent = panel is not null && panels is not null &&
                                 ReferenceEquals(panels.Active, panel) &&
                                 panel.HasProvider(out var currentProvider) &&
                                 ReferenceEquals(currentProvider, detail.Provider);
            var tokenIsCancelled = token.IsCancellationRequested;
            var focusRequestIsInvalid = tokenIsCancelled || attachmentIsCurrent == false ||
                                        panelIsCurrent == false;
            if (focusRequestIsInvalid) {
                return;
            }
            _Preview.TextBox.Focus();
        }));
    }

    protected override void OnLoaded(EventArgs e) {
        Attach();
        base.OnLoaded(e);
    }

    protected override void OnUnloaded(EventArgs e) {
        Detach();
        base.OnUnloaded(e);
    }

    private void Preview_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        if (_Preview.IsLoaded) {
            Attach();
        }
        else {
            Detach();
        }
    }

    private void Attach() {
        if (_Preview.DataContext is not MBoxEntryPreview detail) {
            Detach();
            return;
        }
        if (detail.Provider is not MBoxProvider provider) {
            Detach();
            return;
        }
        var observation = ((IEntryProvider)provider).Observation;
        var attachmentIsCurrent = ReferenceEquals(_Detail, detail) &&
                                  ReferenceEquals(_Provider, provider) &&
                                  ReferenceEquals(_Observation, observation);
        if (attachmentIsCurrent) {
            return;
        }
        Detach();
        _Detail = detail;
        _Provider = provider;
        _Observation = observation;
        _AttachmentId++;
        var attachmentId = _AttachmentId;
        _ObservationChanged = (sender, args) => Observation_CurrentChanged(sender, args, attachmentId);
        observation.CurrentChanged += _ObservationChanged;
        Request();
    }

    private void Detach() {
        var observation = _Observation;
        var observationChanged = _ObservationChanged;
        _ObservationChanged = null;
        _Observation = null;
        _Provider = null;
        _Detail = null;
        var hasObservationHandler = observation is not null && observationChanged is not null;
        if (hasObservationHandler) {
            observation.CurrentChanged -= observationChanged;
        }
        _AttachmentId++;
        Cancel();
        Text = string.Empty;
        Status = string.Empty;
    }

    private void Observation_CurrentChanged(object sender, EventArgs e, long attachmentId) {
        var attachmentIsCurrent = attachmentId == _AttachmentId;
        var senderIsCurrent = ReferenceEquals(sender, _Observation);
        var eventBelongsToCurrentAttachment = attachmentIsCurrent && senderIsCurrent;
        if (eventBelongsToCurrentAttachment) {
            Request();
        }
    }

    private void Cancel() {
        var source = _RequestSource;
        _RequestSource = null;
        if (source == null) {
            return;
        }
        try {
            source.Cancel();
        }
        catch (ObjectDisposedException) {
        }
        finally {
            source.Dispose();
        }
    }

    private void Request() {
        Cancel();
        var attachmentId = _AttachmentId;
        var sequence = ++_RequestSequence;
        var provider = _Provider;
        var observation = _Observation;
        var requestHasCurrentSource = provider is not null && observation is not null;
        if (requestHasCurrentSource == false) {
            Clear();
            return;
        }
        var entry = observation.Current() as MBoxEntry;
        if (entry == null) {
            Clear();
            return;
        }
        var source = new CancellationTokenSource();
        _RequestSource = source;
        Show(MBoxText.Value("MBoxPreview_Status_Loading"), string.Empty);
        var token = source.Token;
        _ = Task.Run(() => Read(provider, entry, attachmentId, sequence, token), token);
    }

    private async Task Read(MBoxProvider provider,
                           MBoxEntry entry,
                           long attachmentId,
                           int sequence,
                           CancellationToken token) {
        MBoxPreviewResult result;
        try {
            result = await provider.Preview(entry, token);
        }
        catch (OperationCanceledException) {
            return;
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(nameof(Read), ex);
            }
            result = MBoxPreviewResult.Empty;
        }
        var generation = provider.ActiveGenerationId;
        await _Dispatcher.InvokeAsync(() => {
            if (token.IsCancellationRequested) {
                return;
            }
            if (attachmentId != _AttachmentId) {
                return;
            }
            if (sequence != _RequestSequence) {
                return;
            }
            if (string.Equals(generation, provider.ActiveGenerationId, StringComparison.Ordinal) == false) {
                return;
            }
            if (string.Equals(entry.GenerationId, generation, StringComparison.Ordinal) == false) {
                return;
            }
            if (ReferenceEquals(_Provider, provider) == false) {
                return;
            }
            Show(result);
        });
    }

    private bool IsCurrentAttachment(MBoxEntryPreview detail, long attachmentId, long generation) {
        var isLoaded = _Preview.IsLoaded;
        var attachmentIdIsCurrent = attachmentId == _AttachmentId;
        var detailIsCurrent = ReferenceEquals(_Detail, detail);
        var dataContextIsCurrent = ReferenceEquals(_Preview.DataContext, detail);
        var controllerIsCurrent = ReferenceEquals(detail?.Controller, this);
        var managerIsCurrent = detail is not null && detail.Provider.Detail.IsCurrent(detail, generation);
        var isCurrent = isLoaded &&
                        attachmentIdIsCurrent &&
                        detailIsCurrent &&
                        dataContextIsCurrent &&
                        controllerIsCurrent &&
                        managerIsCurrent;
        return isCurrent;
    }

    private void Clear() {
        Text = string.Empty;
        Status = MBoxText.Value("MBoxPreview_Status_NoSelection");
    }

    private void Show(MBoxPreviewResult result) {
        switch (result.State) {
            case MBoxBodyState.Complete:
            {
                var isHtmlOnly = result.Text is null && result.Html is not null;
                if (isHtmlOnly) {
                    Text = string.Empty;
                    Status = MBoxText.Value("MBoxPreview_Status_HtmlOnly");
                    break;
                }
                Text = result.Text ?? string.Empty;
                Status = string.Empty;
                break;
            }
            case MBoxBodyState.Truncated:
                Text = result.Text ?? string.Empty;
                Status = MBoxText.Value("MBoxPreview_Status_Truncated");
                break;
            case MBoxBodyState.NotRead:
                Text = string.Empty;
                Status = MBoxText.Value("MBoxPreview_Status_NotRead");
                break;
            case MBoxBodyState.Malformed:
                Text = result.Text ?? string.Empty;
                Status = MBoxText.Value("MBoxPreview_Status_Malformed");
                break;
            default:
                Text = string.Empty;
                Status = MBoxText.Value("MBoxPreview_Status_Unavailable");
                break;
        }
    }

    private void Show(string status, string text) {
        Status = status;
        Text = text;
    }
}
