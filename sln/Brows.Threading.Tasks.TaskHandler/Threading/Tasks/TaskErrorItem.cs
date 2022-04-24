using System;

namespace Brows.Threading.Tasks {
    using ComponentModel;

    public class TaskErrorItem : NotifyPropertyChanged {
        public string Trace { get; }
        public DateTime Time { get; }

        public TaskErrorItem(DateTime time, string trace) {
            Time = time;
            Trace = trace;
        }
    }
}
