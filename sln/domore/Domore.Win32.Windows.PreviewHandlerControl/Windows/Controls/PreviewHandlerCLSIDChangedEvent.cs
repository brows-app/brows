using System;
using System.Windows;

namespace Domore.Windows.Controls {
    public delegate void PreviewHandlerCLSIDChangedEventHandler(object sender, PreviewHandlerCLSIDChangedEventArgs e);

    public sealed class PreviewHandlerCLSIDChangedEventArgs : RoutedEventArgs {
        public string Extension { get; }
        public Guid CLSID { get; }
        public Guid? Override { get; set; }

        public PreviewHandlerCLSIDChangedEventArgs(string extension, Guid clsid, RoutedEvent routedEvent) : base(routedEvent) {
            Extension = extension;
            CLSID = clsid;
        }
    }
}
