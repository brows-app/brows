using Domore.Notification;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class PanelCollection : Notifier, IPanelCollection {
        private readonly List<Panel> List = new();
        private readonly PanelHistoryShared SharedHistory = new();

        private void Activating(Panel panel) {
            var
            passive = (Passive = Active) as Panel;
            passive?.Deactivate();
            Active = panel;
        }

        private bool Closed(Panel panel) {
            if (panel != null) {
                panel.Activating = null;
                panel.Close();
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

        public Panel this[int index] =>
            List[index];

        public Panel Active {
            get => _Active;
            private set => Change(ref _Active, value, nameof(Active));
        }
        private Panel _Active;

        public Panel Passive {
            get => _Passive;
            private set => Change(ref _Passive, value, nameof(Passive));
        }
        private Panel _Passive;

        public IReadOnlyCollection<string> History =>
            SharedHistory.Values;

        public int Count =>
            List.Count;

        public Commander Commander { get; }
        public EntryProviderFactorySet Providers { get; }

        public PanelCollection(EntryProviderFactorySet providers, Commander commander) {
            Providers = providers;
            Commander = commander;
        }

        public async Task<IPanel> Add(string id, CancellationToken token) {
            var panel = Panel(column: List.Count);
            var opened = await panel.Provide(id, token);
            if (opened) {
                List.Add(panel);
                panel.Activate();
                return panel;
            }
            return null;
        }

        public void Remove(IPanel item) {
            if (item is Panel panel) {
                var closed = Closed(panel);
                if (closed) {
                    if (List.Remove(panel)) {
                        var active = default(Panel);
                        var passive = default(Panel);
                        var column = 0;
                        var panels = List.ToList();
                        foreach (var p in panels) {
                            p.Column = column++;
                            if (Active == item && (p.Column == item.Column)) {
                                active = p;
                            }
                            if (Passive == item && (p.Column == item.Column)) {
                                passive = p;
                            }
                        }
                        if (Active == item) {
                            Active = null;
                            if (active == null) {
                                active = panels.LastOrDefault();
                            }
                        }
                        if (Passive == item) {
                            Passive = passive;
                        }
                        if (active != null) {
                            active.Activate();
                        }
                    }
                }
            }
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
    }
}
