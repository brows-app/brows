using System;

namespace Domore.Runtime.InteropServices {
    internal delegate void PreviewWorkerCLSIDChangedEventHandler(object sender, PreviewWorkerCLSIDChangedEventArgs e);

    internal class PreviewWorkerCLSIDChangedEventArgs : EventArgs {
        public string Extension { get; }
        public Guid CLSID { get; }
        public Guid? Override { get; set; }

        public PreviewWorkerCLSIDChangedEventArgs(string extension, Guid clsid) {
            Extension = extension;
            CLSID = clsid;
        }
    }
}
