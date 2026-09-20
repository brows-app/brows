using System;

namespace Brows.IO.Helpers;

public delegate void DirectoryInfoReadyEventHandler(object sender, DirectoryInfoReadyEventArgs e);

public sealed class DirectoryInfoReadyEventArgs : EventArgs {
}
