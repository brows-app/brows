using System;
using System.Collections.Generic;

namespace Brows {
    internal abstract class FtpCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(FtpProvider);

        protected override IEnumerable<Type> Source => new[] {
            typeof(IEntryObservation),
            typeof(FtpEntry)
        };
    }
}
