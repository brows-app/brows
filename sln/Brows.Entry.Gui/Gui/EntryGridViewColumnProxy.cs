using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Gui {
    using Windows.Data;

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
            var key = Key;
            var resolver = Resolver;
            if (resolver != null) {
                yield return resolver.For(key);
            }
            yield return "EntryData_" + key;
        }

        private GridViewColumn GetColumn() {
            var column = default(GridViewColumn);
            var resourceKeys = ResourceKeys();
            foreach (var resourceKey in resourceKeys) {
                var resource = Application.TryFindResource(resourceKey);
                if (resource != null) {
                    if (resource is DataTemplate dataTemplate) {
                        column = new EntryGridViewColumn {
                            CellTemplate = dataTemplate
                        };
                    }
                }
                if (column != null) {
                    break;
                }
            }
            if (column == null) {
                column = new EntryGridViewColumn {
                    DisplayMemberBinding = new Binding(BindingValuePath) {
                        Converter = Converter == null
                            ? null
                            : EntryValueConverter.From(Converter)
                    },
                    Width = Width
                };
            }
            if (column.Header == null) {
                column.Header = new EntryGridViewColumnHeader { Key = Key };
            }
            return column;
        }

        public string BindingValuePath =>
            _BindingValuePath ?? (
            _BindingValuePath = $"Item[{Key}].Value");
        private string _BindingValuePath;

        public string Key { get; }
        public double Width { get; set; } = double.NaN;
        public IEntryDataConverter Converter { get; }
        public IComponentResourceKey Resolver { get; }

        public EntryGridViewColumnProxy(string key, IComponentResourceKey resolver, IEntryDataConverter converter) {
            Key = key;
            Resolver = resolver;
            Converter = converter;
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
