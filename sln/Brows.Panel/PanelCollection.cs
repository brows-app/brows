using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Collections.ObjectModel;

    public class PanelCollection : CollectionSource<IPanel>, IPanelCollection {
        private readonly PanelHistoryShared SharedHistory = new PanelHistoryShared();

        private void Activating(Panel panel) {
            var
            passive = (Passive = Active) as Panel;
            passive?.Deactivate();
            Active = panel;
        }

        private async Task<bool> Closed(Panel panel, CancellationToken cancellationToken) {
            if (panel != null) {
                panel.Activating = null;
                await panel.Close(cancellationToken);
                return true;
            }
            return false;
        }

        public IPanel Active {
            get => _Active;
            private set => Change(ref _Active, value, nameof(Active));
        }
        private IPanel _Active;

        public IPanel Passive {
            get => _Passive;
            private set => Change(ref _Passive, value, nameof(Passive));
        }
        private IPanel _Passive;

        public IReadOnlyCollection<string> History =>
            SharedHistory.Values;

        public async Task<IPanel> Add(string id, IDialogState dialog, IOperationCollection operations, EntryProviderFactoryCollection providerFactory, CancellationToken cancellationToken) {
            var panel = new Panel(providerFactory) {
                Dialog = dialog,
                Operations = operations,
                Activating = Activating,
                Column = List.Count,
                SharedHistory = SharedHistory
            };

            await panel.Open(id, cancellationToken);

            List.Add(panel);
            panel.Activate();

            return panel;
        }

        public async Task Remove(IPanel item, CancellationToken cancellationToken) {
            if (null == item) throw new ArgumentNullException(nameof(item));

            var closed = await Closed(item as Panel, cancellationToken);
            if (closed) {
                if (List.Remove(item)) {
                    var active = default(Panel);
                    var passive = default(Panel);
                    var column = 0;
                    var panels = List.Cast<Panel>().ToList();
                    foreach (var panel in panels) {
                        panel.Column = column++;
                        if (Active == item && (panel.Column == item.Column)) {
                            active = panel;
                        }
                        if (Passive == item && (panel.Column == item.Column)) {
                            passive = panel;
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
}
