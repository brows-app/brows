using Brows.Detail;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class TerminalOpen : FileSystemCommand<TerminalOpenParameter> {
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
        var currentTerminal = provider.Detail.Active as FileSystemTerminal;
        var activeKeyIsTerminal = Equals(provider.Detail.ActiveKey, FileSystemTerminal.Key);
        var terminalIsReusable = currentTerminal is not null &&
                                 activeKeyIsTerminal &&
                                 currentTerminal.IsInvalidated == false &&
                                 ReferenceEquals(currentTerminal.Panel, active) &&
                                 ReferenceEquals(currentTerminal.PanelCollection, panels);
        var terminal = terminalIsReusable
            ? currentTerminal
            : new FileSystemTerminal(provider, panels, active);
        return context.Operate(async (progress, token) => {
            var requestGeneration = terminal.BeginRequest();
            if (requestGeneration < 0) {
                return false;
            }
            var shown = await provider.Detail.ShowAsync(FileSystemTerminal.Key, terminal, token);
            if (shown == false) {
                return false;
            }
            var terminalIsInvalidated = terminal.IsInvalidated;
            if (terminalIsInvalidated) {
                await provider.Detail.CloseAsync(terminal, CancellationToken.None);
                return false;
            }
            var generation = provider.Detail.Generation;
            if (provider.Detail.IsCurrent(terminal, generation) == false) {
                return false;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, terminal.LifetimeToken);
            var controller = await terminal.WaitForController(TimeSpan.FromSeconds(2), linked.Token);
            if (controller is null) {
                return false;
            }
            var requestIsCurrent = terminal.IsCurrentRequest(requestGeneration);
            var detailIsCurrent = provider.Detail.IsCurrent(terminal, generation);
            var openRequestIsStale = requestIsCurrent == false || detailIsCurrent == false;
            if (openRequestIsStale) {
                return false;
            }
            var opened = await controller.OpenAsync(linked.Token);
            if (opened == false) {
                return false;
            }
            var detailRemainsCurrent = provider.Detail.IsCurrent(terminal, generation);
            var requestRemainsCurrent = terminal.IsCurrentRequest(requestGeneration);
            var openRemainsCurrent = detailRemainsCurrent && requestRemainsCurrent;
            if (openRemainsCurrent == false) {
                return false;
            }
            controller.RequestFocusLater(generation, requestGeneration, linked.Token);
            return true;
        });
    }
}
