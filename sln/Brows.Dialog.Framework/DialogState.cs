using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    public class DialogState : IDialogState {
        private object Dialog;
        private readonly IList Dialogs = new List<object>();
        private readonly object DialogLocker = new object();
        private readonly Queue<Action> ActionQueue = new Queue<Action>();

        private Task<T> GetOrAddTask<T>(Func<T, bool> predicate, Func<T> factory, TaskCompletionSource<T> completionSource) where T : IAcknowledge {
            if (null == factory) throw new ArgumentNullException(nameof(factory));
            if (null == completionSource) throw new ArgumentNullException(nameof(completionSource));

            lock (DialogLocker) {
                var dialog = Dialogs.OfType<T>().FirstOrDefault(predicate);
                if (dialog != null) {
                    completionSource.SetResult(dialog);
                    return completionSource.Task;
                }

                if (Dialog != null) {
                    ActionQueue.Enqueue(() => GetOrAddTask(predicate, factory, completionSource));
                    return completionSource.Task;
                }

                void acknowledged(object sender, AcknowledgedEventArgs e) {
                    completionSource.SetResult(dialog);

                    var actionQueue = default(Queue<Action>);
                    lock (DialogLocker) {
                        Manager.Hide();
                        Dialog = null;

                        actionQueue = new Queue<Action>(ActionQueue);
                        ActionQueue.Clear();
                    }

                    while (actionQueue.TryDequeue(out var action)) {
                        action();
                    }

                    dialog.Acknowledged -= acknowledged;
                }

                dialog = factory();
                dialog.Acknowledged += acknowledged;
                Dialog = dialog;
                Dialogs.Add(dialog);
                Manager.Show(dialog);
            }

            return completionSource.Task;
        }

        public object Window { get; set; }
        public IDialog Current => Manager.Current;
        public IDialogManager Manager { get; }

        public DialogState(IDialogManager manager) {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public async Task<T> GetOrAddAsync<T>(Func<T, bool> predicate, Func<T> factory) where T : IAcknowledge {
            return await GetOrAddTask(predicate, factory, new TaskCompletionSource<T>());
        }
    }
}
