using System.Windows;
using System.Windows.Data;

namespace Brows.Gui {
    internal class EntryGridViewColumnCellTemplate {
        public static DataTemplate Build(string key, IEntryColumn info) {
            var
            factory = new FrameworkElementFactory(typeof(EntryDataControl));
            factory.Name = nameof(EntryDataControl);
            factory.SetBinding(EntryDataControl.DataContextProperty, new Binding($"Item[{key}]"));
            return new DataTemplate {
                VisualTree = factory
            };
        }
    }
}
