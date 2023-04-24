using System;
using System.Windows.Input;

namespace Brows.Gui {
    internal sealed class CommandPaletteInputController : Controller<ICommandPaletteInputController>, ICommandPaletteInputController {
        private void Element_Loaded(object sender, System.Windows.RoutedEventArgs e) {
            if (Element.Text.SelectionLength == 0) {
                Element.Text.CaretIndex = Element.Text.Text.Length;
            }
            Element.Text.Focus();
        }

        private void Element_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                e.Trigger(a => Gesture?.Invoke(this, a));
            }
        }

        public event GestureEventHandler Gesture;

        public new CommandPaletteInputControl Element { get; }

        public CommandPaletteInputController(CommandPaletteInputControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.Loaded += Element_Loaded;
            Element.Text.PreviewKeyDown += Element_PreviewKeyDown;
        }

        void ICommandPaletteInputController.MoveCaret(int index) {
            Element.Text.CaretIndex = index;
        }

        void ICommandPaletteInputController.SelectText(int start, int length) {
            Element.Text.Select(start, length);
        }

        void ICommandPaletteInputController.Focus() {
            Element.Text.Focus();
        }
    }
}
