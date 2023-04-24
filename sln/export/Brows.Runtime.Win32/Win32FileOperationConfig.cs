using Domore.Runtime.Win32;

namespace Brows {
    internal class Win32FileOperationConfig {
        public bool AddUndoRecord { get; set; }
        public bool AllowUndo { get; set; }
        public bool EarlyFailure { get; set; }
        public bool NoConfirmation { get; set; }
        public bool NoErrorUI { get; set; }
        public bool PreserveFileExtensions { get; set; }
        public bool RecycleOnDelete { get; set; } = true;
        public bool RenameOnCollision { get; set; }
        public bool Silent { get; set; }

        public uint Flags(Win32FileOperation op) {
            var fof = FOF.NOCONFIRMMKDIR;
            if (NoConfirmation) fof |= FOF.NOCONFIRMATION;
            if (NoErrorUI) fof |= FOF.NOERRORUI;
            if (Silent) fof |= FOF.SILENT;
            if (AllowUndo) fof |= FOF.ALLOWUNDO;
            if (RenameOnCollision) fof |= FOF.RENAMEONCOLLISION;

            var fofx = FOFX.SHOWELEVATIONPROMPT;
            if (EarlyFailure) fofx |= FOFX.EARLYFAILURE;
            if (AddUndoRecord) fofx |= FOFX.ADDUNDORECORD;
            if (RecycleOnDelete) fofx |= FOFX.RECYCLEONDELETE;
            if (PreserveFileExtensions) fofx |= FOFX.PRESERVEFILEEXTENSIONS;

            if (op != null) {
                if (op.PreserveFileExtensions == true) {
                    fofx |= FOFX.PRESERVEFILEEXTENSIONS;
                }
                if (op.PreserveFileExtensions == false) {
                    fofx &= ~FOFX.PRESERVEFILEEXTENSIONS;
                }
                if (op.RecycleOnDelete == true) {
                    fofx |= FOFX.RECYCLEONDELETE;
                }
                if (op.RecycleOnDelete == false) {
                    fofx &= ~FOFX.RECYCLEONDELETE;
                }
                if (op.RenameOnCollision == true) {
                    fof |= FOF.RENAMEONCOLLISION;
                }
                if (op.RenameOnCollision == false) {
                    fof &= ~FOF.RENAMEONCOLLISION;
                }
            }

            return (uint)fof | (uint)fofx;
        }
    }
}
