using Brows.Detail;
using Domore.Conf.Cli;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class Terminal : FileSystemCommand<Terminal.Parameter> {
    private static bool Open(Context context, IPanel active, IPanelCollection panels, FileSystemProvider provider) {
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

    private static bool Release(Context context,
                                IPanel active,
                                IPanelCollection panels,
                                FileSystemProvider provider,
                                FileSystemTerminal terminal) {
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

    private static bool Close(Context context, FileSystemProvider provider, FileSystemTerminal terminal) {
        return context.Operate(async (progress, token) => {
            var closed = await provider.Detail.CloseAsync(terminal, token);
            if (closed) {
                return true;
            }
            return ReferenceEquals(provider.Detail.Active, terminal) == false;
        });
    }

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
        var parameterExists = context.HasParameter(out var parameter);
        var parameterIsInvalid = parameterExists == false && context.HasParameterError(out _);
        if (parameterIsInvalid) {
            return false;
        }
        var terminal = provider.Detail.Active as FileSystemTerminal;
        if (parameter?.Action == Action.Close) {
            /*
             * Closing when no terminal exists has nothing to do.
             */
            if (terminal is null) {
                return true;
            }
            return Close(context, provider, terminal);
        }
        /*
         * With no action or the default action, release focus from a terminal
         * that owns it. Otherwise, focus the existing terminal or open a new one.
         */
        var terminalOwnsFocus = terminal is not null && ((IProviderDetailFocus)terminal).HasKeyboardFocus;
        if (terminalOwnsFocus) {
            return Release(context, active, panels, provider, terminal);
        }
        return Open(context, active, panels, provider);
    }

    public enum Action {
        Default = 0,
        Close
    }

    public sealed class Parameter {
        [CliArgument]
        public Action? Action { get; set; }
    }
}
