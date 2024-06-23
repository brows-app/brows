using System;

namespace Brows {
    public abstract class TriggerEventArgs : EventArgs {
        protected TriggerEventArgs(object source) {
            Source = source;
        }

        public bool Triggered { get; set; }

        public object Source { get; }
    }
}
