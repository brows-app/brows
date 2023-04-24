using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows.Gui {
    internal class CommandPaletteController : Controller<ICommandPaletteController>, ICommandPaletteController {
        private void Element_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                e.Trigger(a => Gesture?.Invoke(this, a));
            }
        }

        public event GestureEventHandler Gesture;

        public new CommandPaletteControl Element { get; }

        public CommandPaletteController(CommandPaletteControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.PreviewKeyDown += Element_PreviewKeyDown;
        }

        void ICommandPaletteController.ScrollSuggestionData(PressKey key) {
            var listBox = Element?.SuggestionDataControl?.FindVisualChild<ListBox>();
            var scrollViewer = Element?.SuggestionDataControl?.FindVisualChild<ScrollViewer>();
            switch (key) {
                case PressKey.Down:
                    if (listBox != null) {
                        listBox.MoveDown();
                    }
                    else {
                        scrollViewer?.LineDown();
                    }
                    break;
                case PressKey.Up:
                    if (listBox != null) {
                        listBox.MoveUp();
                    }
                    else {
                        scrollViewer?.LineUp();
                    }
                    break;
                case PressKey.PageDown:
                    if (listBox != null) {
                        listBox.MovePageDown();
                    }
                    else {
                        scrollViewer?.PageDown();
                    }
                    break;
                case PressKey.PageUp:
                    if (listBox != null) {
                        listBox.MovePageUp();
                    }
                    else {
                        scrollViewer?.PageUp();
                    }
                    break;
            }
        }
    }
}
