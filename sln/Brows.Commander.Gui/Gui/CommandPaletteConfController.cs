using System;
using System.Windows.Input;

namespace Brows.Gui {
    internal sealed class CommandPaletteConfController : Controller<ICommandPaletteConfController>, ICommandPaletteConfController {
        private void Element_KeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                e.Trigger(a => Gesture?.Invoke(this, a));
            }
        }

        public new CommandPaletteConfControl Element { get; }

        public CommandPaletteConfController(CommandPaletteConfControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.Text.PreviewKeyDown += Element_KeyDown;
        }

        public event GestureEventHandler Gesture;

        void ICommandPaletteConfController.Focus() {
            Element.Text.Focus();
        }

        int ICommandPaletteConfController.CaretLine() {
            return Element.Text.GetLineIndexFromCharacterIndex(Element.Text.CaretIndex);
        }
    }
}
