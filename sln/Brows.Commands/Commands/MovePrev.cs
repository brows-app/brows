using System.Collections.Generic;

namespace Brows.Commands {
    using Triggers;

    internal class MovePrev : MoveMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Previous;

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("moveprev", "movep", "mp");
                yield return new KeyboardTrigger(KeyboardKey.M, KeyboardModifiers.Control | KeyboardModifiers.Shift);
            }
        }
    }
}
