using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class PanelData {
        private class Panel : Notifier {
            public string ID {
                get => _ID;
                set => Change(ref _ID, value, nameof(ID));
            }
            private string _ID;

            public IReadOnlyList<string> Columns {
                get => _Columns ?? (_Columns = new List<string>());
                set => Change(ref _Columns, value, nameof(Columns));
            }
            private IReadOnlyList<string> _Columns;

            public IReadOnlyDictionary<string, EntrySortDirection> Sorting {
                get => _Sorting ?? (_Sorting = new Dictionary<string, EntrySortDirection>());
                set => Change(ref _Sorting, value, nameof(Sorting));
            }
            private IReadOnlyDictionary<string, EntrySortDirection> _Sorting;
        }

        private static async Task<string> ID(IEntryProvider provider, CancellationToken cancellationToken) {
            if (provider == null) {
                return null;
            }
            var panelID = provider.PanelID;
            if (panelID == null) {
                return null;
            }
            var id = panelID.Value;
            if (id == null) {
                return null;
            }
            var caseSensitive = await provider.CaseSensitive(cancellationToken);
            return caseSensitive
                ? id
                : id.ToUpper();
        }

        public async Task<bool> Load(IEntryProvider provider, EntryCollection entries, CancellationToken cancellationToken) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            var id = await ID(provider, cancellationToken);
            var manager = Configure.Data<Panel>(id);
            var config = await manager.Load(cancellationToken);
            var columns = config.Columns;
            var configured = false;
            if (columns.Count > 0) {
                configured = true;
                entries.ClearColumns();
                entries.AddColumns(columns.ToArray());
            }
            var sorting = config.Sorting;
            if (sorting.Count > 0) {
                configured = true;
                entries.ClearSort();
                entries.SortColumns(sorting);
            }
            return configured;
        }

        public async Task<bool> Save(IEntryProvider provider, EntryCollection entries, CancellationToken cancellationToken) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            var id = await ID(provider, cancellationToken);
            var manager = Configure.Data<Panel>(id);
            var columns = entries.GetColumns();
            var sorting = entries.Sorting;
            var config = await manager.Load(cancellationToken);
            config.ID = id;
            config.Columns = new List<string>(columns);
            config.Sorting = new Dictionary<string, EntrySortDirection>(sorting.Where(sort => sort.Value.HasValue).ToDictionary(sort => sort.Key, sort => sort.Value.Value));
            return true;
        }
    }
}
