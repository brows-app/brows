namespace Brows.Commands {
    internal class CopyNext : CopyMode, ICommandExport {
        protected override PanelPassiveMode Mode => PanelPassiveMode.Next;
    }
}
