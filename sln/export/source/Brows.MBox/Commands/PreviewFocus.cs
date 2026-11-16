using Brows.Detail;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Moves the keyboard focus between the list of the messages of a mailbox and their preview.
/// </summary>
internal sealed class PreviewFocus : MBoxCommand<PreviewFocusParameter> {
    private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(2);

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanels(out var panels) == false) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasProvider(out MBoxProvider provider) == false) {
            return false;
        }
        if (parameter.Target == PreviewFocusTarget.Entries) {
            return FocusEntries(provider);
        }
        return FocusPreview(context, active, panels, provider);
    }

    /// <summary>
    /// Gets the types of the objects that the command can be triggered by.
    /// </summary>
    /// <remarks>
    /// The command moves the focus of the panel, and needs nothing but the provider of the
    /// panel, so it is not bound to a selected message.
    /// </remarks>
    protected override IEnumerable<Type> Source =>
        null;

    private static bool FocusEntries(MBoxProvider provider) {
        var detail = provider.Detail.Active as MBoxEntryPreview;
        if (detail is null) {
            return false;
        }
        if (detail is not IProviderDetailFocus focus || focus.HasKeyboardFocus == false) {
            /*
             * The preview does not own the focus, so the list keeps it.
             */
            return false;
        }
        return ((IEntryProvider)provider).Observation.Focus();
    }

    private static bool FocusPreview(Context context,
                                     IPanel panel,
                                     IPanelCollection panels,
                                     MBoxProvider provider) {
        var detail = provider.Detail.Active as MBoxEntryPreview;
        if (detail is null) {
            /*
             * The preview is shown, so that it can take the focus.
             */
            detail = new MBoxEntryPreview(provider);
        }
        return context.Operate(async (progress, token) => {
            bool shown;
            try {
                shown = await provider.Detail.ShowAsync(MBoxViews.Preview, detail, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return false;
            }
            if (shown == false) {
                return false;
            }
            var generation = provider.Detail.Generation;
            var controller = await detail.WaitForController(AttachTimeout, token);
            if (controller is null) {
                /*
                 * The preview was never shown, so it cannot take the focus.
                 */
                return false;
            }
            var managerIsCurrent = provider.Detail.IsCurrent(detail, generation);
            var tokenIsCancelled = token.IsCancellationRequested;
            var controllerIsDetached = ReferenceEquals(detail.Controller, controller) == false;
            var panelIsCurrent = ReferenceEquals(panels.Active, panel) &&
                                 panel.HasProvider(out var currentProvider) &&
                                 ReferenceEquals(currentProvider, provider);
            var focusRequestIsInvalid = tokenIsCancelled || managerIsCurrent == false ||
                                        controllerIsDetached || panelIsCurrent == false;
            if (focusRequestIsInvalid) {
                /*
                 * The preview was closed or replaced while its controller was attached, so the
                 * focus stays where it is.
                 */
                return false;
            }
            /*
             * The focus is moved after the work of the user interface that is running now ends,
             * because the panel takes the focus back when the palette closes.
             */
            controller.RequestFocusLater(panel, panels, token);
            return true;
        });
    }
}
