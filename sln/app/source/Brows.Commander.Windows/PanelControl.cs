using Brows.Panels;
using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Input;

namespace Brows;

internal sealed partial class PanelControl : ModelView<IPanel, IPanelController>, IPanelController {
    private event EventHandler _Drop;
    private event EventHandler _FocusedChanged;
    private event EventHandler _SizeChanged;
    private event EventHandler _Activated;

    private IPanelDrop _Dropped;

    private void This_Drop(object sender, DragEventArgs e) {
        _Dropped = new ProviderDrop(ViewModel, e);
        try {
            _Drop?.Invoke(this, e);
        }
        finally {
            _Dropped = null;
        }
    }

    private void This_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
    }

    private void This_DragOver(object sender, DragEventArgs e) {
    }

    private void This_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) {
    }

    private void This_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) {
        _FocusedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void This_SizeChanged(object sender, SizeChangedEventArgs e) {
        _SizeChanged?.Invoke(this, e);
    }

    private void This_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        _Activated?.Invoke(this, e);
    }

    public PanelControl() {
        AllowDrop = true;
        Drop += This_Drop;
        DragOver += This_DragOver;
        GiveFeedback += This_GiveFeedback;
        IsKeyboardFocusWithinChanged += This_IsKeyboardFocusWithinChanged;
        PreviewMouseDown += This_PreviewMouseDown;
        QueryContinueDrag += This_QueryContinueDrag;
        SizeChanged += This_SizeChanged;
    }

    event EventHandler IPanelController.Drop { add => _Drop += value; remove => _Drop -= value; }
    event EventHandler IPanelController.FocusedChanged { add => _FocusedChanged += value; remove => _FocusedChanged -= value; }
    event EventHandler IPanelController.SizeChanged { add => _SizeChanged += value; remove => _SizeChanged -= value; }
    event EventHandler IPanelController.Activated { add => _Activated += value; remove => _Activated -= value; }

    bool IPanelController.Focused => IsKeyboardFocusWithin;
    double IPanelController.Width => ActualWidth;
    double IPanelController.Height => ActualHeight;
    IPanelDrop IPanelController.Dropped => _Dropped;
}
