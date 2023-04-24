using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;

namespace Brows.Gui {
    internal sealed class EntryGridViewColumnCellTemplate {
        private static readonly Dictionary<IEntryDataDefinition, DataTemplate> Cache = new();

        private static DataTemplate Build(IEntryDataDefinition data) {
            if (null == data) throw new ArgumentNullException(nameof(data));
            var
            factory = new FrameworkElementFactory(typeof(EntryDataControl));
            factory.Name = nameof(EntryDataControl);
            factory.SetBinding(EntryDataControl.DataContextProperty, new Binding($"Item[{data.Key}]"));
            return new DataTemplate {
                VisualTree = factory
            };
        }

        public static DataTemplate Get(IEntryDataDefinition data) {
            if (Cache.TryGetValue(data, out var template) == false) {
                Cache[data] = template = Build(data);
            }
            return template;
        }
    }
}
