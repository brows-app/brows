using System;

namespace Domore.IO {
    public delegate void DirectoryInfoReadyEventHandler(object sender, DirectoryInfoReadyEventArgs e);

    public sealed class DirectoryInfoReadyEventArgs : EventArgs {
    }
}
