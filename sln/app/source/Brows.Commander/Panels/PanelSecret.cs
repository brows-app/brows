using Brows.Gui;
using Brows.Instantiation;
using Brows.Localization;
using Brows.Requests;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows.Panels;

internal abstract class PanelSecret : Notifier, IPanelSecret {
    private static readonly IReadOnlyList<Type> Implementations =
        Implementation<IPanelSecret>.From(Assembly.GetExecutingAssembly());

    private readonly TaskCompletionSource<PanelSecret> TaskCompletionSource = new();

    internal static PanelSecret Create<TSecret>(IRequestFactory request)
    where TSecret : IPanelSecret {
        var implementation = Implementations.FirstOrDefault(type => typeof(TSecret).IsAssignableFrom(type));
        if (implementation is null) {
            throw new InvalidOperationException($"No implementation of type '{typeof(TSecret)}' exists.");
        }
        var instance = Activator.CreateInstance(implementation, request) as PanelSecret;
        if (instance is null) {
            throw new InvalidOperationException($"An implementation of type '{typeof(TSecret)}' must extend '{typeof(PanelSecret)}'.");
        }
        return instance;
    }

    internal virtual byte[] Encode() {
        return null;
    }

    internal virtual void Decode(byte[] data) {
    }

    protected abstract void Submit();
    protected abstract void Cancel();

    public object SubmitRequest => field ??= Request.Create(
        owner: this,
        canExecute: _ => true,
        dependencies: [],
        execute: _ => {
            Submit();
            TaskCompletionSource.TrySetResult(this);
        });

    public object CancelRequest => field ??= Request.Create(
        owner: this,
        canExecute: _ => true,
        dependencies: [],
        execute: _ => {
            Cancel();
            TaskCompletionSource.TrySetResult(null);
        });

    public string Prompt =>
        string.Format(
            format: Translation.Global.Value(PromptFormat),
            args: PromptArgs?.ToArray() ?? []);

    public string PromptFormat {
        get;
        set => Change(ref field, value, nameof(PromptFormat), nameof(Prompt));
    }

    public IEnumerable<string> PromptArgs {
        get;
        set => Change(ref field, value?.ToArray(), nameof(PromptArgs), nameof(Prompt));
    }

    public IRequestFactory Request { get; }

    public abstract void Focus();

    public async Task<PanelSecret> Submitted(CancellationToken token) {
        using var _ = token.Register(useSynchronizationContext: true, callback: () => {
            Cancel();
            TaskCompletionSource.TrySetCanceled(token);
        });
        return await TaskCompletionSource.Task;
    }

    protected PanelSecret(IRequestFactory request) {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }
}

internal abstract class PanelSecret<TController> : PanelSecret, IControlled<TController>
where TController : class, IPanelSecretController {
    protected TController Controller { get; private set; }

    protected PanelSecret(IRequestFactory request) : base(request) {
    }

    public sealed override void Focus() {
        Controller?.Focus();
    }

    TController IControlled<TController>.Controller {
        set {
            var newValue = value;
            var oldValue = Controller;
            if (oldValue != newValue) {
                if (oldValue != null) {
                }
                if (newValue != null) {
                    newValue.Focus();
                }
                Controller = newValue;
            }
        }
    }
}
