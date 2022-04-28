using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace Brows.Gui {
    using Triggers;
    using Windows.Input;

    internal class CommanderController : Controller<ICommanderController>, ICommanderController {
        private Window Window;
        private object WindowHandle;

        private void Window_Closed(object sender, EventArgs e) {
            WindowClosed?.Invoke(this, e);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                var key = (KeyboardKey)e.ReferencedKey();
                var modifiers = (KeyboardModifiers)e.ModifierKeys();
                var eventArgs = new KeyboardKeyEventArgs(key, modifiers);
                var eventHandler = WindowKeyboardKeyDown;
                if (eventHandler != null) {
                    eventHandler.Invoke(this, eventArgs);
                }
                var handled = e.Handled = eventArgs.Triggered;
                if (handled == false) {
                    if (key == KeyboardKey.Space) {
                        var input = new InputEventArgs(" ");
                        var handler = WindowInput;
                        if (handler != null) {
                            handler.Invoke(this, input);
                        }
                        e.Handled = input.Triggered;
                    }
                }
            }
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e != null) {
                var text = e.Text;
                var args = new InputEventArgs(text);
                var handler = WindowInput;
                if (handler != null) {
                    handler.Invoke(this, args);
                }
                e.Handled = args.Triggered;
            }
        }

        protected override void OnLoaded(EventArgs e) {
            Window = Window.GetWindow(UserControl);
            Window.Closed += Window_Closed;
            Window.PreviewKeyDown += Window_PreviewKeyDown;
            Window.PreviewTextInput += Window_PreviewTextInput;
            WindowHandle = new WindowInteropHelper(Window).Handle;
            base.OnLoaded(e);
        }

        protected override void OnUnloaded(EventArgs e) {
            var window = Window;
            if (window != null) {
                window.Closed -= Window_Closed;
                window.PreviewKeyDown -= Window_PreviewKeyDown;
                window.PreviewTextInput -= Window_PreviewTextInput;
            }
            Window = null;
            WindowHandle = null;
            base.OnUnloaded(e);
        }

        public event EventHandler WindowClosed;
        public event InputEventHandler WindowInput;
        public event KeyboardKeyEventHandler WindowKeyboardKeyDown;

        public new CommanderControl UserControl { get; }

        public CommanderController(CommanderControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }

        void ICommanderController.CloseWindow() {
            var window = Window;
            if (window != null) window.Close();
        }

        object ICommanderController.NativeWindow() {
            return WindowHandle;
        }

        void ICommanderController.AddPanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));

            var grid = UserControl.PanelGrid;
            var control = new PanelControl();
            control.DataContext = panel;
            control.SetBinding(Grid.ColumnProperty, nameof(panel.Column));

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(control);

            var splitter = grid.ColumnDefinitions.Count > 1 ? new PanelGridSplitter() : null;
            if (splitter != null) {
                splitter.DataContext = panel;
                splitter.SetBinding(Grid.ColumnProperty, nameof(panel.Column));
                control.Padding = new Thickness(splitter.Width, 0, 0, 0);
                grid.Children.Add(splitter);
            }
            foreach (var columnDefinition in grid.ColumnDefinitions) {
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        void ICommanderController.RemovePanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));

            var grid = UserControl.PanelGrid;
            var children = grid.Children;
            for (var i = 0; i < children.Count; i++) {
                var child = children[i] as FrameworkElement;
                if (child != null) {
                    var p = child.DataContext as IPanel;
                    if (p != null) {
                        var orphaned = p == panel;
                        if (orphaned) {
                            children.RemoveAt(i--);
                            BindingOperations.ClearBinding(child, Grid.ColumnProperty);
                            child.DataContext = null;
                        }
                        else {
                            if (p.Column == 0) {
                                var splitter = child as PanelGridSplitter;
                                if (splitter != null) {
                                    splitter.DataContext = null;
                                    BindingOperations.ClearBinding(splitter, Grid.ColumnProperty);
                                    children.RemoveAt(i--);
                                }
                                var control = child as PanelControl;
                                if (control != null) {
                                    control.Padding = new Thickness(0);
                                }
                            }
                        }
                    }
                }
            }
            var columnDefinitions = grid.ColumnDefinitions;
            var columnDefinitionsCount = columnDefinitions.Count;
            if (columnDefinitionsCount > 0) {
                columnDefinitions.RemoveAt(columnDefinitionsCount - 1);
            }
            foreach (var columnDefinition in grid.ColumnDefinitions) {
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }
}
