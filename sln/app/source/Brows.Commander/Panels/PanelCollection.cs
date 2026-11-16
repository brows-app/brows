using Brows.Gui;
using Brows.Providers;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Panels;

internal sealed class PanelCollection : Notifier, IPanelCollection, IControlled<IPanelCollectionController> {
    private readonly SemaphoreSlim LifecycleGate = new(1, 1);
    private readonly Lock ClearLocker = new();
    private readonly CancellationTokenSource Shutdown = new();
    private readonly CancellationToken ShutdownToken;
    private Task ClearTask;
    private volatile bool Ending;
    private readonly List<Panel> List = [];
    /*
     * Startup leaves this set only after admission or successful cleanup. Failed
     * unadmitted panels remain owned here so shutdown can retry their disposal.
     */
    private readonly Dictionary<Panel, Task> Starting = [];
    private readonly PanelHistoryShared SharedHistory = new();

    private void Activating(Panel panel) {
        var
        passive = (Passive = Active) as Panel;
        passive?.Deactivate();
        Active = panel;
    }

    private async Task<bool> Closed(Panel panel) {
        if (panel != null) {
            await panel.CloseAsync();
            panel.Activating = null;
            return true;
        }
        return false;
    }

    private Panel Panel(int column) {
        return new Panel(Providers, Commander) {
            Activating = Activating,
            Column = column,
            SharedHistory = SharedHistory
        };
    }

    private async Task<bool> RemoveCoreAsync(IPanel item) {
        if (item is not Panel panel) {
            return false;
        }
        if (List.Contains(panel) == false) {
            return false;
        }
        var closed = await Closed(panel);
        if (closed == false) {
            return false;
        }
        var removed = List.Remove(panel);
        if (removed == false) {
            return false;
        }
        for (var column = 0; column < List.Count; column++) {
            List[column].Column = column;
        }
        var replacement = List.Count == 0 ? null : List[Math.Min(panel.Column, List.Count - 1)];
        if (Active == panel) {
            Active = null;
            replacement?.Activate();
        }
        var passiveWasRemoved = Passive == panel;
        var passiveIsStale = Passive is not null && List.Contains(Passive) == false;
        var passiveNeedsReplacement = passiveWasRemoved || passiveIsStale;
        if (passiveNeedsReplacement) {
            Passive = replacement == Active ? null : replacement;
        }
        Controller?.RemovePanel(panel);
        return true;
    }

    private async Task ClearCoreAsync() {
        await Task.Yield();
        Shutdown.Cancel();
        Task[] starting;
        lock (ClearLocker) {
            starting = Starting.Values.ToArray();
        }
        await Task.WhenAll(starting);
        await LifecycleGate.WaitAsync();
        try {
            Panel[] pending;
            lock (ClearLocker) {
                pending = Starting.Keys.ToArray();
            }
            foreach (var panel in pending) {
                await panel.CloseAsync();
                lock (ClearLocker) {
                    Starting.Remove(panel);
                }
            }
            foreach (var item in List.ToList()) {
                await RemoveCoreAsync(item);
            }
            Shutdown.Dispose();
        }
        finally {
            LifecycleGate.Release();
        }
    }

    public Panel this[int index] =>
        List[index];

    public Panel Active {
        get;
        private set => Change(ref field, value, nameof(Active));
    }

    public Panel Passive {
        get;
        private set => Change(ref field, value, nameof(Passive));
    }

    public IReadOnlyCollection<string> History =>
        SharedHistory.Values;

    public int Count =>
        List.Count;

    public Commander Commander { get; }
    public ProviderFactorySet Providers { get; }

    public PanelCollection(ProviderFactorySet providers, Commander commander) {
        ShutdownToken = Shutdown.Token;
        Providers = providers;
        Commander = commander;
    }

    public async Task<IPanel> Add(string id, CancellationToken token) {
        Panel panel;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (ClearLocker) {
            if (Ending) {
                return null;
            }
            panel = Panel(column: 0);
            Starting.Add(panel, completion.Task);
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, ShutdownToken);
        var admitted = false;
        var cleanupSucceeded = false;
        try {
            var opened = await panel.Provide(id, linked.Token);
            await LifecycleGate.WaitAsync(linked.Token);
            try {
                var canAdd = opened && Ending == false;
                if (canAdd == false) {
                    return null;
                }
                panel.Column = List.Count;
                List.Add(panel);
                admitted = true;
                panel.Activate();
                Controller?.AddPanel(panel);
                return panel;
            }
            finally {
                LifecycleGate.Release();
            }
        }
        finally {
            try {
                if (admitted == false) {
                    await panel.CloseAsync();
                    cleanupSucceeded = true;
                }
            }
            finally {
                lock (ClearLocker) {
                    var startupIsSettled = admitted || cleanupSucceeded;
                    if (startupIsSettled) {
                        Starting.Remove(panel);
                    }
                    completion.TrySetResult();
                }
            }
        }
    }
    public async Task<bool> Remove(IPanel item) {
        await LifecycleGate.WaitAsync();
        try {
            return await RemoveCoreAsync(item);
        }
        finally {
            LifecycleGate.Release();
        }
    }

    public Task ClearAsync() {
        lock (ClearLocker) {
            var canRetry = ClearTask is null || ClearTask.IsFaulted || ClearTask.IsCanceled;
            if (canRetry) {
                Ending = true;
                ClearTask = ClearCoreAsync();
            }
            return ClearTask;
        }
    }
    public async Task<bool> Shift(IPanel item, int column) {
        if (item.Column == column) {
            return false;
        }
        if (item is not Panel panel) {
            return false;
        }
        if (List.Contains(panel) == false) {
            return false;
        }
        List.Sort((p1, p2) => {
            var c1 = p1.Column;
            var c2 = p2.Column;
            if (p1 == panel) {
                c1 = column;
            }
            if (p2 == panel) {
                c2 = column;
            }
            var compared = Comparer<int>.Default.Compare(c1, c2);
            if (compared != 0) {
                return compared;
            }
            if (panel == p1 && panel.Column < column) {
                return +1;
            }
            if (panel == p1 && panel.Column > column) {
                return -1;
            }
            if (panel == p2 && panel.Column < column) {
                return -1;
            }
            if (panel == p2 && panel.Column > column) {
                return +1;
            }
            return 0;
        });
        for (var i = 0; i < List.Count; i++) {
            List[i].Column = i;
        }
        return await Task.FromResult(true);
    }

    public IList<Panel> ToList() {
        return List.ToList();
    }

    public IEnumerator<Panel> GetEnumerator() {
        return List.GetEnumerator();
    }

    IPanel IPanelCollection.this[int index] => this[index];
    IPanel IPanelCollection.Active => Active;
    IPanel IPanelCollection.Passive => Passive;

    bool IPanelCollection.HasColumn(int column, out IPanel panel) {
        panel = List.FirstOrDefault(item => item.Column == column);
        return panel != null;
    }

    IEnumerator<IPanel> IPanelCollection.GetEnumerator() {
        return List.GetEnumerator();
    }

    IEnumerable<IPanel> IPanelCollection.AsEnumerable() {
        return List.AsEnumerable();
    }

    IPanelCollectionController IControlled<IPanelCollectionController>.Controller {
        set {
            var newValue = value;
            var oldValue = Controller;
            if (oldValue != newValue) {
                if (oldValue != null) {
                    oldValue.RemovePanels(List);
                }
                if (newValue != null) {
                    newValue.AddPanels(List);
                }
                Controller = newValue;
            }
        }
    }
    private IPanelCollectionController Controller;
}
