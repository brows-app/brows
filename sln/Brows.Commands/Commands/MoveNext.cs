namespace Brows.Commands {
    internal class MoveNext : MoveMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Next;
    }
}
