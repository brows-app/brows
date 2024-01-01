using System;
using System.Collections.Generic;

namespace Brows.SSHConnection {
    internal abstract class SSHConnectionCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(SSHConnectionProvider);

        protected override IEnumerable<Type> Source => new[] {
            typeof(IEntryObservation),
            typeof(SSHConnectionEntry)
        };
    }
}
