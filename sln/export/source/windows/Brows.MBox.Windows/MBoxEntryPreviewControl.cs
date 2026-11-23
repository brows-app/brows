using Brows.Detail;
using Brows.MBox.Format;
using Brows.Windows.Controls;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Brows;

/*
 * The preview shows the message that is current when the observation of the provider raises
 * its change event. Every request has a sequence number, so that a request that finishes late
 * never overwrites the result of a newer request. The requests are read on a background task,
 * and every change of the view is made on the dispatcher of the control.
 */
[TemplatePart(Name = PartAttachmentsName, Type = typeof(ItemsControl))]
[TemplatePart(Name = PartAttachmentsLabelName, Type = typeof(TextBlock))]
[TemplatePart(Name = PartBodyName, Type = typeof(TextBox))]
[TemplatePart(Name = PartStatusName, Type = typeof(TextBlock))]
internal sealed class MBoxEntryPreviewControl : ModelView<MBoxEntryPreview, IMBoxEntryPreviewController>,
                                                IMBoxEntryPreviewController {
    private const string PartAttachmentsName = "PART_Attachments";
    private const string PartAttachmentsLabelName = "PART_AttachmentsLabel";
    private const string PartBodyName = "PART_Body";
    private const string PartStatusName = "PART_Status";

    private static readonly ILog Log = Logging.For(typeof(MBoxEntryPreviewControl));

    private ItemsControl AttachmentList;
    private EventHandler ObservationChanged;
    private MBoxEntryPreview Detail;
    private IEntryObservation Observation;
    private MBoxProvider Provider;
    private CancellationTokenSource RequestSource;
    private int RequestSequence;
    private long AttachmentId;

    private event EventHandler ControllerLoaded;

    private void This_Loaded(object sender, RoutedEventArgs e) {
        Attach();
        ControllerLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        Detach();
    }

    private void Attach() {
        var detail = ViewModel;
        if (detail is null) {
            Detach();
            return;
        }
        if (detail.Provider is not MBoxProvider provider) {
            Detach();
            return;
        }
        var observation = ((IEntryProvider)provider).Observation;
        var attachmentIsCurrent = ReferenceEquals(Detail, detail) &&
                                  ReferenceEquals(Provider, provider) &&
                                  ReferenceEquals(Observation, observation);
        if (attachmentIsCurrent) {
            return;
        }
        Detach();
        Detail = detail;
        Provider = provider;
        Observation = observation;
        AttachmentId++;
        var attachmentId = AttachmentId;
        ObservationChanged = (sender, args) => Observation_CurrentChanged(sender, args, attachmentId);
        observation.CurrentChanged += ObservationChanged;
        Request();
    }

    private void Detach() {
        var observation = Observation;
        var observationChanged = ObservationChanged;
        ObservationChanged = null;
        Observation = null;
        Provider = null;
        Detail = null;
        var hasObservationHandler = observation is not null && observationChanged is not null;
        if (hasObservationHandler) {
            observation.CurrentChanged -= observationChanged;
        }
        AttachmentId++;
        Cancel();
        Text = string.Empty;
        Status = string.Empty;
        Attachments = Array.Empty<MBoxPreviewAttachment>();
    }

    private void Observation_CurrentChanged(object sender, EventArgs e, long attachmentId) {
        var attachmentIsCurrent = attachmentId == AttachmentId;
        var senderIsCurrent = ReferenceEquals(sender, Observation);
        var eventBelongsToCurrentAttachment = attachmentIsCurrent && senderIsCurrent;
        if (eventBelongsToCurrentAttachment) {
            Request();
        }
    }

    private void Cancel() {
        var source = RequestSource;
        RequestSource = null;
        if (source is null) {
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
        var attachmentId = AttachmentId;
        var sequence = ++RequestSequence;
        var provider = Provider;
        var observation = Observation;
        var requestHasCurrentSource = provider is not null && observation is not null;
        if (requestHasCurrentSource == false) {
            Clear();
            return;
        }
        var entry = observation.Current() as MBoxEntry;
        if (entry is null) {
            Clear();
            return;
        }
        var source = new CancellationTokenSource();
        RequestSource = source;
        Show(MBoxText.Value(Translation, "MBoxPreview_Status_Loading"), string.Empty);
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
        await Dispatcher.InvokeAsync(() => {
            if (token.IsCancellationRequested) {
                return;
            }
            if (attachmentId != AttachmentId) {
                return;
            }
            if (sequence != RequestSequence) {
                return;
            }
            if (string.Equals(generation, provider.ActiveGenerationId, StringComparison.Ordinal) == false) {
                return;
            }
            if (string.Equals(entry.GenerationId, generation, StringComparison.Ordinal) == false) {
                return;
            }
            if (ReferenceEquals(Provider, provider) == false) {
                return;
            }
            Show(result);
        });
    }

    private bool IsCurrentAttachment(MBoxEntryPreview detail, long attachmentId, long generation) {
        var isLoaded = IsLoaded;
        var attachmentIdIsCurrent = attachmentId == AttachmentId;
        var detailIsCurrent = ReferenceEquals(Detail, detail);
        var viewModelIsCurrent = ReferenceEquals(ViewModel, detail);
        var controllerIsCurrent = ReferenceEquals(detail?.Controller, this);
        var managerIsCurrent = detail is not null && detail.Provider.Detail.IsCurrent(detail, generation);
        var isCurrent = isLoaded &&
                        attachmentIdIsCurrent &&
                        detailIsCurrent &&
                        viewModelIsCurrent &&
                        controllerIsCurrent &&
                        managerIsCurrent;
        return isCurrent;
    }

    private bool FocusBody() {
        var body = BodyTextBox;
        if (body is null) {
            return false;
        }
        return body.Focus();
    }

    private void Clear() {
        Text = string.Empty;
        Status = MBoxText.Value(Translation, "MBoxPreview_Status_NoSelection");
        Attachments = Array.Empty<MBoxPreviewAttachment>();
    }

    private void Show(MBoxPreviewResult result) {
        /*
         * The attachments are listed whatever the state of the body, so that a message whose
         * body cannot be shown still tells what it holds.
         */
        Attachments = result.Attachments ?? Array.Empty<MBoxPreviewAttachment>();
        switch (result.State) {
            case MBoxBodyState.Complete: {
                var isHtmlOnly = result.Text is null && result.Html is not null;
                if (isHtmlOnly) {
                    Text = string.Empty;
                    Status = MBoxText.Value(Translation, "MBoxPreview_Status_HtmlOnly");
                    break;
                }
                Text = result.Text ?? string.Empty;
                Status = string.Empty;
                break;
            }
            case MBoxBodyState.Truncated:
                Text = result.Text ?? string.Empty;
                Status = MBoxText.Value(Translation, "MBoxPreview_Status_Truncated");
                break;
            case MBoxBodyState.NotRead:
                Text = string.Empty;
                Status = MBoxText.Value(Translation, "MBoxPreview_Status_NotRead");
                break;
            case MBoxBodyState.Malformed:
                Text = result.Text ?? string.Empty;
                Status = MBoxText.Value(Translation, "MBoxPreview_Status_Malformed");
                break;
            default:
                Text = string.Empty;
                Status = MBoxText.Value(Translation, "MBoxPreview_Status_Unavailable");
                break;
        }
    }

    private void Show(string status, string text) {
        Status = status;
        Text = text;
        Attachments = Array.Empty<MBoxPreviewAttachment>();
    }

    internal IReadOnlyList<MBoxPreviewAttachment> Attachments {
        get;
        private set {
            field = value ?? Array.Empty<MBoxPreviewAttachment>();
            var list = AttachmentList;
            if (list is not null) {
                list.ItemsSource = field;
            }
        }
    } = Array.Empty<MBoxPreviewAttachment>();

    internal TextBox BodyTextBox { get; private set; }
    internal TextBlock StatusTextBlock { get; private set; }

    internal ITranslationExport Translation { get; set; }

    internal string Text {
        get;
        private set {
            field = value ?? string.Empty;
            var body = BodyTextBox;
            if (body is not null) {
                body.Text = field;
            }
        }
    } = string.Empty;

    internal string Status {
        get;
        private set {
            field = value ?? string.Empty;
            var status = StatusTextBlock;
            if (status is not null) {
                status.Text = field;
            }
        }
    } = string.Empty;

    protected sealed override void OnViewModelChanged(MBoxEntryPreview oldValue, MBoxEntryPreview newValue) {
        base.OnViewModelChanged(oldValue, newValue);
        if (IsLoaded) {
            Attach();
            return;
        }
        Detach();
    }

    public bool HasKeyboardFocus =>
        IsKeyboardFocusWithin;

    public MBoxEntryPreviewControl() {
        Imports.Current.Populate(this);
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    public sealed override void OnApplyTemplate() {
        base.OnApplyTemplate();
        BodyTextBox = GetTemplateChild(PartBodyName) as TextBox;
        StatusTextBlock = GetTemplateChild(PartStatusName) as TextBlock;
        if (BodyTextBox is not null) {
            BodyTextBox.Text = Text;
        }
        if (StatusTextBlock is not null) {
            StatusTextBlock.Text = Status;
        }
        AttachmentList = GetTemplateChild(PartAttachmentsName) as ItemsControl;
        if (AttachmentList is not null) {
            AttachmentList.ItemsSource = Attachments;
        }
        if (GetTemplateChild(PartAttachmentsLabelName) is TextBlock attachmentsLabel) {
            attachmentsLabel.Text = MBoxText.Value(Translation, "MBoxPreview_Attachments");
        }
    }

    public bool RequestFocus() {
        var detail = Detail;
        if (detail is null) {
            return false;
        }
        var attachmentId = AttachmentId;
        var generation = detail.Provider.Detail.Generation;
        var isCurrentAttachment = IsCurrentAttachment(detail, attachmentId, generation);
        if (isCurrentAttachment == false) {
            return false;
        }
        return FocusBody();
    }

    public void RequestFocusLater(IPanel panel, IPanelCollection panels, CancellationToken token) {
        /*
         * The dispatcher does not interrupt the work item that is running now, so the preview
         * takes the focus after the palette closed and the panel gave the focus back to the
         * list.
         */
        var detail = Detail;
        if (detail is null) {
            return;
        }
        var attachmentId = AttachmentId;
        var generation = detail.Provider.Detail.Generation;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
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
            FocusBody();
        }));
    }

    event EventHandler IController.Loaded {
        add => ControllerLoaded += value;
        remove => ControllerLoaded -= value;
    }
}
