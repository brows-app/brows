using Domore.Runtime.Win32;
using System;

namespace Brows.IO.Extensions {
    internal static class PayloadExtension {
        public static uint Win32Flags(this IPayload payload) {
            if (null == payload) throw new ArgumentNullException(nameof(payload));

            var f = payload;
            var fof = FOF.NOCONFIRMMKDIR;
            if (f.NativeConfirmation == false) fof |= FOF.NOCONFIRMATION;
            if (f.NativeError == false) fof |= FOF.NOERRORUI;
            if (f.NativeProgress == false) fof |= FOF.SILENT;
            if (f.NativeUndo) fof |= FOF.ALLOWUNDO;
            if (f.NativeRenameOnCollision) fof |= FOF.RENAMEONCOLLISION;

            var fofx = FOFX.SHOWELEVATIONPROMPT;
            if (f.NativeFailEarly) fofx |= FOFX.EARLYFAILURE;
            if (f.NativeUndo) fofx |= FOFX.ADDUNDORECORD;
            if (f.NativeTrash) fofx |= FOFX.RECYCLEONDELETE;
            if (f.NativeRenameOnCollision) fofx |= FOFX.PRESERVEFILEEXTENSIONS;

            return (uint)fof | (uint)fofx;
        }
    }
}
