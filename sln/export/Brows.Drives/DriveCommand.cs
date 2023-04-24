using System;

namespace Brows {
    public class DriveCommand<TParameter> : Command<TParameter> where TParameter : new() {
        protected sealed override Type Provider =>
            typeof(DriveProvider);
    }
}
