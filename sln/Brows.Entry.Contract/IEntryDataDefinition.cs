﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryDataDefinition {
        string Key { get; }
        string Name { get; }
        string Group { get; }
        double Width { get; }
        EntryDataAlignment Alignment { get; }
        IEntryDataConverter Converter { get; }
        object ConverterParameter { get; }
        IExportResourceKey ResourceKey { get; }
        Task<object> GetValue(IEntry entry, Action<object> progress, CancellationToken cancellationToken);
        int CompareValue(IEntry x, IEntry y);
        void RefreshValue(IEntry entry);
        bool SuggestKey(ICommandContext context);
    }
}
