namespace Brows.Commands {
    internal class CopyPrev : CopyMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Previous;
    }
}
