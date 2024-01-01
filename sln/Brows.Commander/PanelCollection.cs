using Brows.Gui;
using Domore.Notification;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class PanelCollection : Notifier, IPanelCollection, IControlled<IPanelCollectionController> {
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
        public ProviderFactorySet Providers { get; }

        public PanelCollection(ProviderFactorySet providers, Commander commander) {
            Providers = providers;
            Commander = commander;
        }

        public async Task<IPanel> Add(string id, CancellationToken token) {
            var panel = Panel(column: List.Count);
            var opened = await panel.Provide(id, token);
            if (opened) {
                List.Add(panel);
                panel.Activate();
                Controller?.AddPanel(panel);
                return panel;
            }
            return null;
        }

        public async Task<bool> Remove(IPanel item) {
            if (item is not Panel panel) {
                return false;
            }
            var closed = Closed(panel);
            if (closed == false) {
                return false;
            }
            var removed = List.Remove(panel);
            if (removed == false) {
                return false;
            }
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
            Controller?.RemovePanel(panel);
            return await Task.FromResult(true);
        }

        public void Clear() {
            foreach (var item in List) {
                var closed = Closed(item);
                if (closed) {
                    Controller?.RemovePanel(item);
                }
            }
            List.Clear();
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
}
