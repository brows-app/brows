using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class PanelSecret : Notifier, IControlled<IPanelSecretController>, IControllingGesture, IControllingText {
        private readonly TaskCompletionSource<SecureString> TaskCompletionSource = new();

        public object SubmitRequest => Request.Create(
            execute: _ => {
                SecureString = Controller?.SecretString?.Copy();
                SecureString?.MakeReadOnly();
                Controller?.Clear();
                TaskCompletionSource.TrySetResult(SecureString);
            });

        public object CancelRequest => Request.Create(
            execute: _ => {
                SecureString = null;
                Controller?.Clear();
                TaskCompletionSource.TrySetResult(null);
            });

        public SecureString SecureString { get; private set; }

        public string Prompt =>
            string.Format(
                format: Translation.Global.Value(PromptFormat),
                args: PromptArgs?.ToArray() ?? Array.Empty<string>());

        public string PromptFormat {
            get => _PromptFormat;
            set => Change(ref _PromptFormat, value, nameof(PromptFormat), nameof(Prompt));
        }
        private string _PromptFormat;

        public IEnumerable<string> PromptArgs {
            get => _PromptArgs;
            set => Change(ref _PromptArgs, value?.ToArray(), nameof(PromptArgs), nameof(Prompt));
        }
        private IEnumerable<string> _PromptArgs;

        public Task<SecureString> Submitted(CancellationToken token) {
            token.Register(useSynchronizationContext: true, callback: () => {
                Controller?.Clear();
                SecureString = null;
                TaskCompletionSource.TrySetCanceled(token);
            });
            return TaskCompletionSource.Task;
        }

        public void Focus() {
            Controller?.Focus();
        }

        IPanelSecretController IControlled<IPanelSecretController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                    }
                    if (newValue != null) {
                    }
                    Controller = newValue;
                }
            }
        }
        private IPanelSecretController Controller;
    }
}
