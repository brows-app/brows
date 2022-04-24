using System.Collections.Generic;

namespace Brows.Commands {
    using Triggers;

    internal class CopyNext : CopyMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Next;

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("copynext", "copyn", "cn");
                yield return new KeyboardTrigger(KeyboardKey.C, KeyboardModifiers.Control | KeyboardModifiers.Alt);
            }
        }
    }
}
