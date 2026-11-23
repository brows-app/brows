using Brows.Localization;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

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
        var column = new EntryGridViewColumn(Key, Agent.Width);
        column.CellTemplate = EntryGridViewColumnCellTemplate.Wrap(column, dataTemplate);
        if (column.Header == null) {
            column.Header = new EntryGridViewColumnHeader(Agent.ResourceKey, Translation) {
                Column = column,
                Display = Agent.Name,
                Key = Agent.Key
            };
        }
        return column;
    }

    public string Key =>
        Agent.Key;

    public IEntryDataDefinition Agent { get; }
    public ITranslation Translation { get; }

    public EntryGridViewColumnProxy(IEntryDataDefinition agent, ITranslation translation) {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Translation = translation;
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
