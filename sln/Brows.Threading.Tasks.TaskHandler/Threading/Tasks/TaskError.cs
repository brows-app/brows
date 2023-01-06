using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;

namespace Brows.Threading.Tasks {
    public abstract class TaskError : Notifier {
        private static readonly ILog Log = Logging.For(typeof(TaskError));

        private readonly List<TaskErrorItem> InstanceList = new List<TaskErrorItem>();
        private static readonly List<TaskErrorItem> StaticList = new List<TaskErrorItem>();

        protected abstract string LogHeader { get; }

        protected void Canceled(OperationCanceledException ex) {
            if (Log.Info()) {
                Log.Info(
                    LogHeader,
                    ex?.GetType()?.Name ?? nameof(Canceled));
            }
        }

        protected void Errored(Exception ex) {
            var item = new TaskErrorItem(DateTime.Now, ex?.ToString() ?? nameof(Errored));
            if (Log.Error()) {
                Log.Error(
                    LogHeader,
                    item.Trace);
            }
            Exception = ex;
            lock (InstanceList) {
                InstanceList.Add(item);
            }
            lock (StaticList) {
                StaticList.Add(item);
            }
        }

        public Exception Exception {
            get => _Exception;
            private set => Change(ref _Exception, value, nameof(Exception));
        }
        private Exception _Exception;

        public IEnumerable<TaskErrorItem> Errors {
            get {
                lock (StaticList) {
                    return new List<TaskErrorItem>(StaticList);
                }
            }
        }
    }
}
