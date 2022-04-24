using System.Collections.Generic;

namespace Brows.Commands {
    using Triggers;

    internal class CopyPrev : CopyMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Previous;

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("copyprev", "copyp", "cp");
                yield return new KeyboardTrigger(KeyboardKey.C, KeyboardModifiers.Control | KeyboardModifiers.Alt | KeyboardModifiers.Shift);
            }
        }
    }
}
