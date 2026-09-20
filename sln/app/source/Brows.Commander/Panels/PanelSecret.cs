using Brows.Gui;
using Brows.Localization;
using Brows.Requests;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace Brows.Panels;

internal sealed class PanelSecret : Notifier, IControlled<IPanelSecretController>,
                                              IControllingGesture,
                                              IControllingText {
    private readonly TaskCompletionSource<SecureString> TaskCompletionSource = new();

    public object SubmitRequest => Request.Create(
        owner: this,
        canExecute: _ => true,
        dependencies: [],
        execute: _ => {
            SecureString = Controller?.SecretString?.Copy();
            SecureString?.MakeReadOnly();
            Controller?.Clear();
            TaskCompletionSource.TrySetResult(SecureString);
        });

    public object CancelRequest => Request.Create(
        owner: this,
        canExecute: _ => true,
        dependencies: [],
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
        get;
        set => Change(ref field, value, nameof(PromptFormat), nameof(Prompt));
    }

    public IEnumerable<string> PromptArgs {
        get;
        set => Change(ref field, value?.ToArray(), nameof(PromptArgs), nameof(Prompt));
    }

    public IRequestFactory Request { get; }

    public PanelSecret(IRequestFactory request) {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

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
