using System.Collections.Generic;

namespace Brows.Commands {
    using Triggers;

    internal class MoveNext : MoveMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Next;

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("movenext", "moven", "mn");
                yield return new KeyboardTrigger(KeyboardKey.M, KeyboardModifiers.Control);
            }
        }
    }
}
