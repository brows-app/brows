# Keyboard input ownership

Views with interactive keyboard input can restrict Brows command routing without
hardcoding shortcuts in their controls. The shared framework contract is
`Brows.Gui.IControllingCommandGesture`. It extends the existing
`IControllingGesture` marker and supplies the owning `IProvider` and an
`Allows(ICommand)` policy. Commands still run through their configured triggers
and the normal command context.

A scoped source is accepted only for the active panel's current provider instance.
Commander does not activate a panel or fall back to the first panel during scoped
dispatch. Commands rejected by the policy cannot run, including when an allowed
command sharing the shortcut declines it. Existing marker-only sources continue
to suppress gestures. A view that also owns text must implement `IControllingText`
to prevent Space, text composition and IME input reaching provider text handling.

Set `Brows.Gui.InputOwner.Owner` on the interactive view subtree, rather than on
an entire panel. For example, a host can call `InputOwner.SetOwner(element, model)`;
XAML may use `xmlns:gui="clr-namespace:Brows.Gui;assembly=Brows.Framework.Windows"`
and `gui:InputOwner.Owner="{Binding}"`. The attached property is inherited so a
package control's internal DataContext does not determine Brows input ownership.
The window resolves keyboard gestures and text through the same owner lookup.
Connected FrameworkElement sources use direct inherited owner/window property reads;
content, detached and root sources retain the defensive fallback. No owner value is
cached across input events, so binding changes and reparenting are immediately visible.
Keyboard dispatch constructs one gesture event without a capturing callback.
Unmarked sibling controls retain ordinary routing. A local or styled null owner
blocks inherited ownership; clearing the local dependency-property value restores
inheritance. Focus fallback is restricted to missing, detached or Window sources
in the same window, so a sibling does not borrow the focused view's owner.

A successfully handled scoped key is reserved until its matching KeyUp, including
repeats after focus moves elsewhere. For text-capable keys, only the next automatic
text event in that sequence is reserved, before matching KeyUp or an unrelated
KeyDown. Multi-step/IME compositions remain with their owner. Arbitrarily synthesized
automatic text has no physical-key identity and follows the current input sequence.
Text timestamps are not compared with key timestamps; [WPF creates text event
timestamps separately](https://source.dot.net/PresentationCore/System/Windows/Input/TextCompositionEventArgs.cs.html).
Window deactivation, unload and window replacement clear the reservation.
Unmatched keys remain available to the interactive control; the input policy itself
does not mark them handled.

The file-system terminal uses this routing boundary and allows only
`FileSystem_TerminalRelease` while its subtree owns input. Open, release, and close
are also available through the command palette. Default shortcuts are configured
in the later shortcut phase; this integration does not install default bindings.

Provider lower views are selected through `Provider.Detail.Active`, with resources
keyed by the concrete model type. Use `ShowAsync` and expected-instance `CloseAsync`
to change views, or `ToggleAsync` with a key and factory to toggle a view. Toggles
are serialized, so repeated invocations preserve their order while the previous
view is closing. Canceling a queued toggle skips it without changing the view.
Do not write a detail dictionary or choose a view by position.
Optional focus and asynchronous lifetime capabilities keep preview and terminal
behavior out of the shared host. A successful replacement awaits the previous
view's cleanup; a cleanup failure retains that view and exposes its error.
The detail owner also joins canceled candidate cleanup during shutdown and retains
failed retirement for retry. An instance whose retirement has begun cannot be
admitted as a new candidate.

Panel navigation, removal, and commander shutdown await lower-view cleanup before
releasing their provider. Native window closure is deferred while asynchronous
cleanup runs, so the WPF dispatcher remains available. Repeated close requests
share cleanup; a failed cleanup keeps the window open and permits a retry.
Panel startup happens outside the collection mutation gate, so opening a slow
provider does not block unrelated panel removal. Shutdown cancels and joins
in-flight panel startup before releasing the collection.

## Validation

The Phase 2 Release/x64 full solution rebuild passed. All 42 application tests and
58 Framework.Windows tests passed, including 28 new cases. The scoped-command
regressions first failed under the old routing; the styled-null boundary regression
also failed before its resolver fix. The complete managed run reported the same
four missing-style failures in FTP.Windows/SFTP.Windows preview tests observed in
Phase 1. Environment-dependent cases were skipped; these failures were not changed.

The tests exercise WPF routed events and deterministic state transitions. They do
not claim OS keyboard-injection, real terminal/TUI or end-user shortcut validation;
those checks belong to the later terminal-host integration.

The subsequent per-key refactor passed all 44 application tests. A warmed allocation
regression measured 2,432,000 bytes for 2,000 mixed inherited-owner/ordinary lookups
in the previous resolver and zero bytes for those lookups on the connected-element
path after the change. This measures resolver allocation, not end-to-end input
latency or the allocations made by command execution and WPF itself.

Phase 1A/3 validation passed the full Release/x64 solution build and all 26
managed projects' latest runs (2,949 passed, zero failed). The explicit terminal
smoke verified the literal provider directory, input/rendering, process reuse
and awaited process exit. Default shortcuts and remaining lifecycle/deployment
checks belong to later phases. See the
[review evidence](../../../../agents/reviews/2026-11-09_Review-023.md), including the unrelated
NativeLoader test flake and its successful full-suite rerun.
