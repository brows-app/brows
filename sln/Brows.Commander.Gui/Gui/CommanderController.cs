﻿using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Brows.Gui {
    internal class CommanderController : Controller<ICommanderController>, ICommanderController {
        private Window Window;
        private object WindowHandle;

        private void Window_Closed(object sender, EventArgs e) {
            WindowClosed?.Invoke(this, e);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                var handled = e.Trigger(a => WindowGesture?.Invoke(this, a));
                if (handled == false) {
                    if (e.Key == Key.Space) {
                        var input = new InputEventArgs(" ", (e.OriginalSource as FrameworkElement)?.DataContext);
                        var handler = WindowInput;
                        if (handler != null) {
                            handler.Invoke(this, input);
                        }
                        e.Handled = input.Triggered;
                    }
                }
            }
        }

        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e != null) {
                var handled = e.Trigger(clicks: 2, a => WindowGesture?.Invoke(this, a));
                if (handled == false) {
                }
            }
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e != null) {
                var text = e.Text;
                var args = new InputEventArgs(text, (e.OriginalSource as FrameworkElement)?.DataContext);
                var handler = WindowInput;
                if (handler != null) {
                    handler.Invoke(this, args);
                }
                e.Handled = args.Triggered;
            }
        }

        protected override void OnLoaded(EventArgs e) {
            Window = Window.GetWindow(Element);
            Window.Closed += Window_Closed;
            Window.PreviewKeyDown += Window_PreviewKeyDown;
            Window.PreviewTextInput += Window_PreviewTextInput;
            Window.PreviewMouseDoubleClick += Window_PreviewMouseDoubleClick;
            WindowHandle = new WindowInteropHelper(Window).Handle;
            base.OnLoaded(e);
        }

        protected override void OnUnloaded(EventArgs e) {
            var window = Window;
            if (window != null) {
                window.Closed -= Window_Closed;
                window.PreviewKeyDown -= Window_PreviewKeyDown;
                window.PreviewTextInput -= Window_PreviewTextInput;
                window.PreviewMouseDoubleClick -= Window_PreviewMouseDoubleClick;
            }
            Window = null;
            WindowHandle = null;
            base.OnUnloaded(e);
        }

        public event EventHandler WindowClosed;
        public event InputEventHandler WindowInput;
        public event GestureEventHandler WindowGesture;

        public new CommanderControl Element { get; }

        public CommanderController(CommanderControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        void ICommanderController.CloseWindow() {
            var window = Window;
            if (window != null) window.Close();
        }

        object ICommanderController.NativeWindow() {
            return WindowHandle;
        }
    }
}
