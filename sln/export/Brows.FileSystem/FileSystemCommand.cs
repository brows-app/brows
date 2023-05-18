using System;
using System.Collections.Generic;

namespace Brows {
    public class FileSystemCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(FileSystemProvider);

        protected override IEnumerable<Type> Source => new[] {
            typeof(IEntryObservation),
            typeof(FileSystemEntry)
        };
    }
}
