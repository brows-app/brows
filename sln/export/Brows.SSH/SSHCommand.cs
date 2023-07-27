using System;
using System.Collections.Generic;

namespace Brows {
    internal abstract class SSHCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(SSHProvider);

        protected override IEnumerable<Type> Source => new[] {
            typeof(IEntryObservation),
            typeof(SSHEntry)
        };
    }
}
