using Domore.Notification;
using System;

namespace Brows.Threading.Tasks {
    public class TaskErrorItem : Notifier {
        public string Trace { get; }
        public DateTime Time { get; }

        public TaskErrorItem(DateTime time, string trace) {
            Time = time;
            Trace = trace;
        }
    }
}
