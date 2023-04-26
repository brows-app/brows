using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Gui {
    internal class EntryGridViewColumnProxy {
        private static Application Application =>
            _Application ?? (
            _Application = Application.Current);
        private static Application _Application;

        private GridViewColumn Column =>
            _Column ?? (
            _Column = GetColumn());
        private GridViewColumn _Column;

        private IEnumerable<object> ResourceKeys() {
            var key = Agent.Key;
            var resolver = Agent.ResourceKey;
            if (resolver != null) {
                yield return resolver.For(key);
            }
            yield return "EntryData_" + key;
        }

        private GridViewColumn GetColumn() {
            var dataTemplate = default(DataTemplate);
            var resourceKeys = ResourceKeys();
            foreach (var resourceKey in resourceKeys) {
                var resource = Application.TryFindResource(resourceKey);
                var template = dataTemplate = resource as DataTemplate;
                if (template != null) {
                    break;
                }
            }
            if (dataTemplate == null) {
                dataTemplate = EntryGridViewColumnCellTemplate.Get(Agent);
            }
            var column = new EntryGridViewColumn {
                CellTemplate = dataTemplate,
                Width = Agent?.Width ?? double.NaN
            };
            if (column.Header == null) {
                column.Header = new EntryGridViewColumnHeader(Agent.ResourceKey) {
                    Display = Agent.Name,
                    Key = Agent.Key
                };
            }
            return column;
        }

        public string Key =>
            Agent.Key;

        public IEntryDataDefinition Agent { get; }

        public EntryGridViewColumnProxy(IEntryDataDefinition agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public void Sorting(EntrySortDirection? direction) {
            var header = Column.Header as EntryGridViewColumnHeader;
            if (header != null) {
                header.Sorting(direction);
            }
        }

        public void AddTo(GridView gridView) {
            if (null == gridView) throw new ArgumentNullException(nameof(gridView));
            gridView.Columns.Add(Column);
        }

        public bool RemoveFrom(GridView gridView) {
            if (null == gridView) throw new ArgumentNullException(nameof(gridView));
            return gridView.Columns.Remove(Column);
        }
    }
}
