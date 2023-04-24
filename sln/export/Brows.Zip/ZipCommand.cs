using System;

namespace Brows {
    public abstract class ZipCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(ZipProvider);
    }
}
