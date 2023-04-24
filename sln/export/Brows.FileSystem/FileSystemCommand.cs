using System;

namespace Brows {
    public class FileSystemCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(FileSystemProvider);
    }
}
