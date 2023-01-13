namespace Brows.Commands {
    internal class MovePrev : MoveMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Previous;
    }
}
