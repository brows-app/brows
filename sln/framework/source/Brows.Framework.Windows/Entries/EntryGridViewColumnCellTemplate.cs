using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Entries;

internal sealed class EntryGridViewColumnCellTemplate {
    private static readonly Dictionary<IEntryDataDefinition, DataTemplate> Cache = new();

    private static DataTemplate Build(IEntryDataDefinition data) {
        if (null == data) throw new ArgumentNullException(nameof(data));
        var
        factory = new FrameworkElementFactory(typeof(EntryDataControl));
        factory.Name = nameof(EntryDataControl);
        factory.SetBinding(EntryDataControl.ViewModelProperty, new Binding($"Item[{data.Key}]"));
        return new DataTemplate {
            VisualTree = factory
        };
    }

    internal static DataTemplate Wrap(EntryGridViewColumn column, DataTemplate template) {
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(ContentPresenter.ContentProperty, new Binding("."));
        content.SetValue(ContentPresenter.ContentTemplateProperty, template);
        var measure = new FrameworkElementFactory(typeof(EntryColumnMeasure));
        measure.SetValue(EntryColumnMeasure.ColumnProperty, column);
        measure.SetBinding(EntryColumnMeasure.DataProperty, new Binding($"Item[{column.DataKey}]"));
        measure.AppendChild(content);
        return new DataTemplate { VisualTree = measure };
    }

    public static DataTemplate Get(IEntryDataDefinition data) {
        if (Cache.TryGetValue(data, out var template) == false) {
            Cache[data] = template = Build(data);
        }
        return template;
    }
}
