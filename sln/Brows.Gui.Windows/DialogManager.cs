using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Brows {
    internal class DialogManager : Notifier, IDialogManager {
        private readonly Stack<IDialog> Stack = new Stack<IDialog>();

        public Application Application { get; }

        public IDialog Current {
            get => _Current;
            private set => Change(ref _Current, value, nameof(Current));
        }
        private IDialog _Current;

        public DialogManager(Application application) {
            Application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public IDialog Show(object data) {
            if (null == data) throw new ArgumentNullException(nameof(data));
            var app = Application;
            var key = data.GetType().Name;
            var rsc = $"Dialog_{key}";
            var tpl = app.TryFindResource(rsc) as ControlTemplate;
            return app.Dispatcher.Invoke(() => {
                Stack.Push(Current = new Dialog {
                    Data = data,
                    Key = key,
                    Template = tpl
                });
                return Current;
            });
        }

        public IDialog Hide() {
            return Application.Dispatcher.Invoke(() => {
                if (Stack.TryPop(out var current)) {
                    if (Stack.TryPeek(out var previous)) {
                        Current = previous;
                    }
                    else {
                        Current = null;
                    }
                    return current;
                }
                return null;
            });
        }
    }
}
