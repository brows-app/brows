using Brows.Detail;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class TerminalRelease : FileSystemCommand<TerminalReleaseParameter> {
    protected sealed override IEnumerable<Type> Source =>
        null;

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.HasPanels(out var panels) == false) {
            return false;
        }
        if (active.HasProvider(out FileSystemProvider provider) == false) {
            return false;
        }
        if (provider.Detail.Active is not FileSystemTerminal terminal) {
            return false;
        }
        var generation = provider.Detail.Generation;
        if (provider.Detail.IsCurrent(terminal, generation) == false) {
            return false;
        }
        var panelCollectionIsCurrent = ReferenceEquals(terminal.PanelCollection, panels);
        var activePanelIsCurrent = ReferenceEquals(panels.Active, active);
        var panelIsCurrent = panelCollectionIsCurrent && activePanelIsCurrent && terminal.IsPanelCurrent;
        if (panelIsCurrent == false) {
            return false;
        }
        var invokedByGesture = context.HasGesture(out _);
        var terminalOwnsFocus = ((IProviderDetailFocus)terminal).HasKeyboardFocus;
        var shortcutReleaseIsInvalid = invokedByGesture && terminalOwnsFocus == false;
        if (shortcutReleaseIsInvalid) {
            return false;
        }
        var releaseIntentGeneration = terminal.AdvanceReleaseIntent();
        var controller = terminal.CurrentController;
        if (controller is null) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var detailIsCurrent = provider.Detail.IsCurrent(terminal, generation);
            var releaseIntentIsCurrent = terminal.IsCurrentReleaseIntent(releaseIntentGeneration);
            var releaseIsCurrent = detailIsCurrent && releaseIntentIsCurrent;
            if (releaseIsCurrent == false) {
                return false;
            }
            return await controller.FocusEntriesAsync(generation, releaseIntentGeneration, token);
        });
    }
}
